// Template Studio. Builds GOV.UK Design System components over a TemplateDocument and writes every change
// straight into the JSON that is saved. No inline handlers: the Content-Security-Policy blocks them.
(function () {
    'use strict';

    var lib = window.FlexFormsTemplateDocument;
    var container = document.getElementById('template-studio');
    var dataElement = document.getElementById('template-studio-data');
    if (!lib || !container || !dataElement) return;

    var data = JSON.parse(dataElement.textContent || '{}');
    var complexFields = data.complexFields || [];

    var FIELD_TYPES = [
        { value: 'text', label: 'Short answer', short: 'Short answer' },
        { value: 'email', label: 'Email address', short: 'Email' },
        { value: 'text-area', label: 'Long answer', short: 'Long answer' },
        { value: 'character-count', label: 'Long answer with a character or word limit', short: 'Long answer (limit)' },
        { value: 'radios', label: 'Choose one option (radios)', short: 'Radios' },
        { value: 'checkboxes', label: 'Choose one or more options (checkboxes)', short: 'Checkboxes' },
        { value: 'select', label: 'Choose one option from a dropdown', short: 'Dropdown' },
        { value: 'date', label: 'Date', short: 'Date' },
        { value: 'search', label: 'Search and select, for example a trust or school', short: 'Search' },
        { value: 'upload', label: 'File upload', short: 'Upload' }
    ];

    var TEXT_TYPES = ['text', 'email', 'text-area', 'character-count'];

    var KIND_LABELS = {
        template: 'Template settings',
        group: 'Section',
        task: 'Task',
        page: 'Page',
        field: 'Question',
        flow: 'Repeatable list',
        derivedFlow: 'Repeated pages'
    };

    var PATTERN_PRESETS = [
        { value: 'custom', label: 'Custom pattern', rule: '', message: '' },
        {
            value: 'ukPhone', label: 'UK telephone number',
            rule: '^(?:0|\\+?44)\\s?(\\d\\s?){9,10}$',
            message: 'Enter a telephone number, like 01632 960 001 or 07700 900 982'
        },
        {
            value: 'postcode', label: 'UK postcode',
            rule: '^[A-Za-z]{1,2}\\d[A-Za-z\\d]?\\s*\\d[A-Za-z]{2}$',
            message: 'Enter a real postcode'
        },
        { value: 'digits', label: 'Whole numbers only', rule: '^\\d+$', message: 'Enter a whole number' },
        { value: 'minimum3', label: 'At least 3 characters', rule: '.{3,}', message: 'Enter 3 characters or more' }
    ];

    var OPERATOR_TEXT = {
        equals: 'is', notEquals: 'is not', 'not_equals': 'is not', 'in': 'is one of', notIn: 'is not one of',
        contains: 'contains', 'not_contains': 'does not contain', startsWith: 'starts with', endsWith: 'ends with',
        greaterThan: 'is more than', lessThan: 'is less than', greaterThanOrEqual: 'is at least',
        lessThanOrEqual: 'is at most', between: 'is between', isEmpty: 'has not been given', isNotEmpty: 'has been given',
        isTrue: 'is true', isFalse: 'is false', hasLength: 'has length', matchesPattern: 'matches',
        isValidEmail: 'is a valid email', isValidPhone: 'is a valid phone number'
    };

    var NO_VALUE_OPERATORS = ['isEmpty', 'isNotEmpty', 'isTrue', 'isFalse', 'isValidEmail', 'isValidPhone'];

    // Comparisons the rule builder offers that don't need an answer to compare with.
    var VALUELESS = ['isEmpty', 'isNotEmpty'];

    var DATE_OPERATOR_TEXT = { lessThan: 'is before', greaterThan: 'is after', lessThanOrEqual: 'is on or before', greaterThanOrEqual: 'is on or after' };

    function operatorText(condition) {
        if (condition.dataType === 'date' && DATE_OPERATOR_TEXT[condition.operator]) return DATE_OPERATOR_TEXT[condition.operator];
        return OPERATOR_TEXT[condition.operator] || condition.operator;
    }

    var tree = document.getElementById('tpl-tree');
    var inspector = document.getElementById('tpl-inspector');
    var rulesPanel = document.getElementById('tpl-rules');
    var problems = document.getElementById('tpl-problems');
    var statusText = document.getElementById('tpl-status');
    var tabsElement = document.getElementById('template-studio-tabs');
    var jsonArea = document.getElementById('tpl-json');
    var jsonGroup = document.getElementById('tpl-json-group');
    var jsonError = document.getElementById('tpl-json-error');
    var saveForm = document.getElementById('tpl-save-form');
    var schemaInput = document.getElementById('NewSchema');
    var previewForm = document.getElementById('tpl-preview-form');
    var tokenInput = saveForm.querySelector('input[name="__RequestVerificationToken"]');

    // Raw template JSON trips the Azure WAF's injection rules, so it's sent as base64 (TemplateStudioSchemaTransport).
    function encodeSchema(text) {
        var bytes = new TextEncoder().encode(text);
        var binary = '';
        for (var i = 0; i < bytes.length; i += 0x8000) {
            binary += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
        }
        return 'b64:' + window.btoa(binary);
    }

    var panels = {
        design: document.getElementById('tpl-panel-design'),
        rules: document.getElementById('tpl-panel-rules'),
        json: document.getElementById('tpl-panel-json')
    };

    var doc = null;
    var selected = [];
    var dirty = !!data.hasUnsavedChanges;
    var jsonDirty = false;
    var submitting = false;
    // Nodes created in this session get their ids from their names until someone edits the id.
    var autoIds = new WeakSet();
    var expanded = new WeakSet();
    var treeLabels = new Map();
    // Which option or format check is open for changing, for example { key: 'option', index: 2 }.
    var editing = null;
    var confirmingDelete = false;
    var moving = false;
    // Undo and redo keep whole copies of the JSON. Typing in one box is a single step.
    var undoStack = [];
    var redoStack = [];
    var committedJson = null;
    var initialJson = null;
    var commitTimer = null;
    var HISTORY_LIMIT = 50;
    // The rule being added or changed in the Rules tab, and the rule waiting for delete confirmation.
    var ruleDraft = null;
    var ruleErrors = {};
    var confirmingRuleDelete = null;

    // The stored version this copy started from. Once applications use the template, reporting compares new versions
    // with it, so removed or changed questions have to be retired rather than lost.
    var baselineRoot = (function () {
        try { return data.baselineSchemaJson ? JSON.parse(data.baselineSchemaJson) : {}; } catch (e) { return {}; }
    })();
    var baselineFields = lib.inventory(baselineRoot).fields;
    var applicationCount = typeof data.applicationCount === 'number' ? data.applicationCount : null;

    function isStarterPlaceholderBaseline() {
        if (data.isStarter || baselineFields.size === 0) return !!data.isStarter;
        if (baselineFields.size !== 1) return false;
        var only = baselineFields.values().next().value;
        return only && only.fieldId === 'starter-text' && !only.parentFieldId;
    }

    // Unknown counts are treated as in use, except for a starter template that nothing has used yet.
    function inUse() {
        if (data.isStarter || applicationCount === 0) return false;
        if (applicationCount == null && isStarterPlaceholderBaseline()) return false;
        return baselineFields.size > 0 && applicationCount !== 0;
    }

    function baselineFor(key) {
        return key ? baselineFields.get(lib.fieldKey(key.parentFieldId, key.fieldId)) || null : null;
    }

    // Answers the tenant's event mappings read. Events are sent without an answer that no longer exists.
    var eventMappings = Array.isArray(data.eventMappings) ? data.eventMappings : [];

    function mappingsReading(fieldIds) {
        var wanted = new Set(fieldIds.map(function (id) { return String(id).toLowerCase(); }));
        return eventMappings.filter(function (reference) { return wanted.has(String(reference.fieldId).toLowerCase()); });
    }

    function describeMapping(reference) {
        return 'the ' + reference.eventType + ' event mapping (' + reference.mappingId + ')';
    }

    // Every answer id at or under a node: questions, and the lists that hold repeated answers.
    function answerIdsIn(node) {
        var ids = [];
        (function walk(value) {
            if (Array.isArray(value)) { value.forEach(walk); return; }
            if (!value || typeof value !== 'object') return;
            if (typeof value.fieldId === 'string' && value.fieldId) ids.push(value.fieldId);
            Object.keys(value).forEach(function (key) { if (key !== 'conditionalLogic') walk(value[key]); });
        })(node);
        return ids;
    }

    function renderMappingWarnings() {
        var area = document.getElementById('tpl-mapping-warnings');
        if (!area || !doc) return;
        var missing = doc.missingMappedFields(eventMappings);
        if (!missing.length) {
            area.replaceChildren();
            return;
        }
        area.replaceChildren(warningText([
            'Event mappings read answers that aren\'t in this version. Events will be sent without them until the mappings are updated in Event mappings:',
            h('ul', { class: 'govuk-list govuk-list--bullet govuk-!-margin-top-2 govuk-!-margin-bottom-0' }, missing.map(function (reference) {
                return h('li', { text: '"' + reference.fieldId + '" in ' + describeMapping(reference) });
            }))
        ]));
    }

    // ---- GOV.UK Frontend ---------------------------------------------------------------------------------------

    // The page template imports GOV.UK Frontend as a module. Importing the same URL shares that instance, so
    // components Template Studio adds after the page has loaded can be initialised the same way.
    function govukFrontendUrl() {
        var scripts = document.querySelectorAll('script[type="module"]');
        for (var i = 0; i < scripts.length; i++) {
            if (scripts[i].src && /govuk-frontend/.test(scripts[i].src)) return scripts[i].src;
            var match = /from\s+['"]([^'"]*govuk-frontend[^'"]*)['"]/.exec(scripts[i].textContent || '');
            if (match) return match[1];
        }
        return '/govuk-frontend.min.js';
    }

    var govuk = null;
    var govukReady = import(govukFrontendUrl())
        .then(function (module) { govuk = module; return module; })
        .catch(function () { return null; });

    function enhance(scope) {
        if (!govuk || !scope) return;
        var options = { scope: scope, onError: function () { } };
        govuk.createAll(govuk.Radios, undefined, options);
        govuk.createAll(govuk.Checkboxes, undefined, options);
        govuk.createAll(govuk.ErrorSummary, undefined, options);
        govuk.createAll(govuk.NotificationBanner, undefined, options);
    }

    // ---- DOM helpers -------------------------------------------------------------------------------------------

    function h(tag, props, children) {
        var element = document.createElement(tag);
        var value;
        Object.keys(props || {}).forEach(function (key) {
            var prop = props[key];
            if (prop == null || prop === false) return;
            if (key === 'class') element.className = prop;
            else if (key === 'text') element.textContent = prop;
            else if (key === 'value') value = prop;
            else if (key === 'checked') element.checked = true;
            else if (/^on[A-Z]/.test(key)) element.addEventListener(key.slice(2).toLowerCase(), prop);
            else element.setAttribute(key, prop === true ? '' : prop);
        });
        append(element, children);
        if (value !== undefined) element.value = value;
        return element;
    }

    function append(element, children) {
        if (children == null || children === false) return;
        if (Array.isArray(children)) {
            children.forEach(function (child) { append(element, child); });
            return;
        }
        element.appendChild(typeof children === 'string' ? document.createTextNode(children) : children);
    }

    function hidden(text) {
        return text ? h('span', { class: 'govuk-visually-hidden', text: ' ' + text }) : null;
    }

    function hint(id, text) {
        return text ? h('div', { class: 'govuk-hint', id: id + '-hint', text: text }) : null;
    }

    function describedBy(id, text, extra) {
        var ids = [text ? id + '-hint' : null, extra].filter(Boolean).join(' ');
        return ids || null;
    }

    function formGroup(children) {
        return h('div', { class: 'govuk-form-group' }, children);
    }

    function textInput(options) {
        var input = h('input', {
            class: 'govuk-input' + (options.width ? ' govuk-input--width-' + options.width : ''),
            id: options.id,
            type: 'text',
            value: options.value == null ? '' : String(options.value),
            inputmode: options.numeric ? 'numeric' : null,
            spellcheck: options.code || options.numeric ? 'false' : null,
            autocomplete: 'off',
            'aria-describedby': describedBy(options.id, options.hint, options.describedBy),
            onFocus: options.onFocus ? function (e) { options.onFocus(e.target.value); } : null,
            onInput: options.onInput ? function (e) { options.onInput(e.target.value, e.target); } : null,
            onChange: options.onChange ? function (e) { options.onChange(e.target.value, e.target); } : null
        });
        return formGroup([
            h('label', { class: 'govuk-label ' + (options.labelClass || 'govuk-label--s'), for: options.id, text: options.label }),
            hint(options.id, options.hint),
            options.after || null,
            options.suffix
                ? h('div', { class: 'govuk-input__wrapper' }, [input, h('div', { class: 'govuk-input__suffix', 'aria-hidden': 'true', text: options.suffix })])
                : input
        ]);
    }

    function textArea(options) {
        return formGroup([
            h('label', { class: 'govuk-label govuk-label--s', for: options.id, text: options.label }),
            hint(options.id, options.hint),
            h('textarea', {
                class: 'govuk-textarea',
                id: options.id,
                rows: String(options.rows || 3),
                spellcheck: options.code ? 'false' : null,
                value: options.value == null ? '' : String(options.value),
                'aria-describedby': describedBy(options.id, options.hint),
                onInput: function (e) { options.onInput(e.target.value); }
            })
        ]);
    }

    function checkbox(options) {
        return formGroup(h('div', { class: 'govuk-checkboxes govuk-checkboxes--small', 'data-module': 'govuk-checkboxes' },
            h('div', { class: 'govuk-checkboxes__item' }, [
                h('input', {
                    class: 'govuk-checkboxes__input',
                    id: options.id,
                    type: 'checkbox',
                    checked: !!options.checked,
                    'aria-describedby': describedBy(options.id, options.hint),
                    onChange: function (e) { options.onChange(e.target.checked); }
                }),
                h('label', { class: 'govuk-label govuk-checkboxes__label', for: options.id, text: options.label }),
                options.hint ? h('div', { class: 'govuk-hint govuk-checkboxes__hint', id: options.id + '-hint', text: options.hint }) : null
            ])));
    }

    // Radios, with GOV.UK conditional reveals for items that have a `conditional` element.
    function radios(options) {
        var group = h('div', {
            class: 'govuk-radios govuk-radios--small' + (options.inline ? ' govuk-radios--inline' : ''),
            'data-module': 'govuk-radios'
        });

        function syncConditionals() {
            group.querySelectorAll('input[aria-controls], input[data-aria-controls]').forEach(function (input) {
                var target = document.getElementById(input.getAttribute('aria-controls') || input.getAttribute('data-aria-controls'));
                if (target) target.classList.toggle('govuk-radios__conditional--hidden', !input.checked);
            });
        }

        options.items.forEach(function (item, index) {
            var id = options.name + '-' + index;
            var conditionalId = item.conditional ? id + '-conditional' : null;
            append(group, h('div', { class: 'govuk-radios__item' }, [
                h('input', {
                    class: 'govuk-radios__input',
                    id: id,
                    name: options.name,
                    type: 'radio',
                    value: item.value,
                    checked: item.value === options.value,
                    'data-aria-controls': conditionalId,
                    'aria-describedby': item.hint ? id + '-hint' : null,
                    onChange: function () {
                        syncConditionals();
                        options.onChange(item.value);
                    }
                }),
                h('label', { class: 'govuk-label govuk-radios__label', for: id, text: item.label }),
                item.hint ? h('div', { class: 'govuk-hint govuk-radios__hint', id: id + '-hint', text: item.hint }) : null
            ]));
            if (item.conditional) {
                append(group, h('div', {
                    class: 'govuk-radios__conditional' + (item.value === options.value ? '' : ' govuk-radios__conditional--hidden'),
                    id: conditionalId
                }, item.conditional));
            }
        });

        return formGroup(h('fieldset', { class: 'govuk-fieldset', 'aria-describedby': describedBy(options.name, options.hint) }, [
            h('legend', { class: 'govuk-fieldset__legend govuk-fieldset__legend--s', text: options.legend }),
            hint(options.name, options.hint),
            group
        ]));
    }

    function selectInput(options) {
        return formGroup([
            h('label', { class: 'govuk-label govuk-label--s', for: options.id, text: options.label }),
            hint(options.id, options.hint),
            h('select', {
                class: 'govuk-select',
                id: options.id,
                value: options.value == null ? '' : options.value,
                'aria-describedby': describedBy(options.id, options.hint),
                onChange: function (e) { options.onChange(e.target.value); }
            }, options.items.map(function (item) {
                return h('option', { value: item.value, text: item.label });
            }))
        ]);
    }

    function button(text, onClick, variant, id) {
        return h('button', {
            type: 'button',
            id: id || null,
            class: 'govuk-button' + (variant ? ' govuk-button--' + variant : ''),
            onClick: onClick
        }, text);
    }

    function actionLink(action) {
        return h('a', {
            href: '#',
            class: 'govuk-link',
            onClick: function (e) {
                e.preventDefault();
                action.onClick();
            }
        }, [action.text, hidden(action.hidden)]);
    }

    // GOV.UK summary list. Rows are { key, value, actions: [{ text, hidden, onClick }] }.
    function summaryList(rows, noBorder) {
        var anyActions = rows.some(function (row) { return row.actions && row.actions.length; });
        return h('dl', { class: 'govuk-summary-list' + (noBorder ? ' govuk-summary-list--no-border' : '') }, rows.map(function (row) {
            var actions = (row.actions || []).filter(Boolean);
            var actionsCell = null;
            if (actions.length === 1) {
                actionsCell = h('dd', { class: 'govuk-summary-list__actions' }, actionLink(actions[0]));
            } else if (actions.length > 1) {
                actionsCell = h('dd', { class: 'govuk-summary-list__actions' }, h('ul', { class: 'govuk-summary-list__actions-list' },
                    actions.map(function (action) {
                        return h('li', { class: 'govuk-summary-list__actions-list-item' }, actionLink(action));
                    })));
            }
            return h('div', { class: 'govuk-summary-list__row' + (anyActions && !actionsCell ? ' govuk-summary-list__row--no-actions' : '') }, [
                h('dt', { class: 'govuk-summary-list__key' }, row.key),
                h('dd', { class: 'govuk-summary-list__value' }, row.value),
                actionsCell
            ]);
        }));
    }

    function tag(text, colour) {
        return h('strong', { class: 'govuk-tag' + (colour ? ' govuk-tag--' + colour : ''), text: text });
    }

    function section(title, children) {
        return h('div', null, [
            h('hr', { class: 'govuk-section-break govuk-section-break--l govuk-section-break--visible' }),
            h('h3', { class: 'govuk-heading-m', text: title }),
            children
        ]);
    }

    function inset(text) {
        return h('div', { class: 'govuk-inset-text', text: text });
    }

    function warningText(content) {
        return h('div', { class: 'govuk-warning-text' }, [
            h('span', { class: 'govuk-warning-text__icon', 'aria-hidden': 'true', text: '!' }),
            h('strong', { class: 'govuk-warning-text__text' }, [h('span', { class: 'govuk-visually-hidden', text: 'Warning' }), content])
        ]);
    }

    function lowerFirst(text) {
        return text ? text.charAt(0).toLowerCase() + text.slice(1) : text;
    }

    function focusById(id) {
        var element = document.getElementById(id);
        if (element) element.focus();
    }

    // ---- Template helpers --------------------------------------------------------------------------------------

    function complexFieldConfig(id) {
        if (!id) return null;
        var lower = id.toLowerCase();
        return complexFields.find(function (config) { return (config.id || '').toLowerCase() === lower; }) || null;
    }

    function isUploadComplexField(id) {
        var config = complexFieldConfig(id);
        if (config) return (config.fieldType || '').toLowerCase() === 'upload';
        return /upload/i.test(id || '');
    }

    function complexFieldIds(kind) {
        return complexFields
            .filter(function (config) { return ((config.fieldType || '').toLowerCase() === 'upload') === (kind === 'upload'); })
            .map(function (config) { return config.id; });
    }

    function editorType(field) {
        if (field.type === 'complexField') {
            return isUploadComplexField(field.complexField && field.complexField.id) ? 'upload' : 'search';
        }
        return field.type;
    }

    function fieldTypeInfo(type) {
        return FIELD_TYPES.find(function (item) { return item.value === type; }) || { value: type, label: type, short: type };
    }

    function rulesOf(field) {
        if (!Array.isArray(field.validations)) field.validations = [];
        return field.validations;
    }

    function findRule(field, types) {
        return (field.validations || []).find(function (rule) {
            return types.indexOf(rule.type) !== -1 && !rule.condition;
        });
    }

    function requiredState(field) {
        if (findRule(field, ['required']) || field.required === true) return 'required';
        if (field.required === false) return 'optional';
        return 'default';
    }

    function isRequired(field) {
        var state = requiredState(field);
        return state === 'required' || (state === 'default' && doc.root.defaultFieldRequirementPolicy === 'required');
    }

    function defaultRequiredMessage(field) {
        var label = ((field.label && field.label.value) || '').trim();
        switch (editorType(field)) {
            case 'radios':
            case 'select': return 'Select an option';
            case 'checkboxes': return 'Select at least one option';
            case 'date': return label ? 'Enter ' + lowerFirst(label) : 'Enter a date';
            case 'upload': return 'Select a file';
            case 'search': return 'Search for and select an option';
            default: return label && !/\?$/.test(label) ? 'Enter ' + lowerFirst(label) : 'Enter an answer';
        }
    }

    function setRequired(field, state, message) {
        var rules = rulesOf(field);
        if (state === 'required') {
            field.required = true;
            var rule = findRule(field, ['required']);
            if (!rule) {
                rule = { type: 'required', rule: true, message: defaultRequiredMessage(field) };
                rules.unshift(rule);
            }
            if (message) rule.message = message;
            return;
        }
        field.validations = rules.filter(function (rule) { return !(rule.type === 'required' && !rule.condition); });
        if (state === 'optional') field.required = false;
        else delete field.required;
    }

    function changeFieldType(field, type) {
        var requiredRule = findRule(field, ['required']);
        var hadDefaultMessage = requiredRule && requiredRule.message === defaultRequiredMessage(field);

        if (type === 'search' || type === 'upload') {
            var keep = field.type === 'complexField' && editorType(field) === type;
            field.type = 'complexField';
            if (!keep) {
                var ids = complexFieldIds(type);
                field.complexField = { id: ids[0] || (type === 'upload' ? 'UploadDocumentsComplexField' : '') };
            }
        } else {
            field.type = type;
            delete field.complexField;
        }

        if (lib.isChoiceType(field.type)) {
            if (!Array.isArray(field.options) || field.options.length === 0) {
                field.options = lib.defaultOptions(field.type);
                field.options.forEach(function (option) { autoIds.add(option); });
            }
        } else {
            delete field.options;
        }

        if (TEXT_TYPES.indexOf(field.type) === -1 && Array.isArray(field.validations)) {
            field.validations = field.validations.filter(function (rule) {
                return rule.type !== 'maxLength' && rule.type !== 'maxWords';
            });
        }

        if (hadDefaultMessage) requiredRule.message = defaultRequiredMessage(field);
    }

    function nodeTitle(path) {
        var node = doc.get(path);
        switch (doc.kindOf(path)) {
            case 'template': return doc.root.templateName || 'Template';
            case 'group': return node.groupName || node.groupId || 'Untitled section';
            case 'task': return node.taskName || node.taskId || 'Untitled task';
            case 'page': return node.title || node.pageId || 'Untitled page';
            case 'field': return (node.label && node.label.value) || node.fieldId || 'Untitled question';
            default: return node.title || node.flowId || 'Untitled list';
        }
    }

    function nodeType(path) {
        var node = doc.get(path);
        switch (doc.kindOf(path)) {
            case 'task':
                var mode = node.summary && node.summary.mode;
                if (mode === 'multiCollectionFlow') return 'Task with repeatable lists';
                if (mode === 'derivedCollectionFlow') return 'Task with repeated pages';
                return 'Task';
            case 'field': return fieldTypeInfo(editorType(node)).short + (isRequired(node) ? '' : ', optional');
            default: return KIND_LABELS[doc.kindOf(path)];
        }
    }

    function childrenOf(path, source) {
        var from = source || doc;
        var node = from.get(path);
        var kind = from.kindOf(path);
        var children = [];
        function add(key) {
            var items = key.reduce(function (value, part) { return value && value[part]; }, node);
            if (Array.isArray(items)) {
                items.forEach(function (_, index) { children.push(path.concat(key, [index])); });
            }
        }
        if (kind === 'template') add(['taskGroups']);
        else if (kind === 'group') add(['tasks']);
        else if (kind === 'task') { add(['pages']); add(['summary', 'flows']); add(['summary', 'derivedFlows']); }
        else if (kind === 'flow' || kind === 'derivedFlow') add(['pages']);
        else if (kind === 'page') add(['fields']);
        return children;
    }

    function ancestorsOf(path) {
        var list = [];
        var current = path;
        while (current.length) {
            current = doc.parentPath(current);
            list.unshift(current);
        }
        return list;
    }

    function ruleTargets() {
        var targets = new Set();
        (doc.root.conditionalLogic || []).forEach(function (rule) {
            if (rule.enabled === false) return;
            (rule.affectedElements || []).forEach(function (element) { targets.add(element.elementId); });
        });
        return targets;
    }

    function idOf(path) {
        var node = doc.get(path);
        switch (doc.kindOf(path)) {
            case 'page': return node.pageId;
            case 'field': return node.fieldId;
            default: return null;
        }
    }

    // ---- State changes -----------------------------------------------------------------------------------------

    function updateStatus() {
        statusText.replaceChildren(h('span', null, dirty
            ? [tag('Unsaved changes', 'yellow'), ' Save a new version to keep them.']
            : 'No unsaved changes'));
    }

    function markDirty() {
        scheduleCommit();
        if (dirty) return;
        dirty = true;
        updateStatus();
    }

    // ---- Undo and redo -----------------------------------------------------------------------------------------

    var liveRegion = h('span', { class: 'govuk-visually-hidden', 'aria-live': 'polite' });
    container.appendChild(liveRegion);

    function announce(text) {
        liveRegion.textContent = '';
        window.setTimeout(function () { liveRegion.textContent = text; }, 50);
    }

    function scheduleCommit() {
        window.clearTimeout(commitTimer);
        commitTimer = window.setTimeout(commitHistory, 500);
    }

    // Records the current JSON as an undo step if it changed since the last one.
    function commitHistory() {
        window.clearTimeout(commitTimer);
        commitTimer = null;
        if (!doc || committedJson === null) return;
        var json = doc.toJson();
        if (json === committedJson) return;
        undoStack.push(committedJson);
        if (undoStack.length > HISTORY_LIMIT) undoStack.shift();
        redoStack = [];
        committedJson = json;
        updateHistoryButtons();
        renderMappingWarnings();
    }

    function updateHistoryButtons() {
        [['tpl-undo', undoStack], ['tpl-redo', redoStack]].forEach(function (pair) {
            var historyButton = document.getElementById(pair[0]);
            if (!historyButton) return;
            historyButton.disabled = !pair[1].length;
            historyButton.setAttribute('aria-disabled', String(!pair[1].length));
        });
    }

    // Keeps the same sections, tasks and pages open after the document is replaced.
    function carryOverExpanded(previous, next) {
        var open = [];
        (function walk(path) {
            if (path.length && expanded.has(previous.get(path))) open.push(path);
            childrenOf(path, previous).forEach(walk);
        })([]);
        expanded = new WeakSet();
        open.forEach(function (path) { if (next.exists(path)) expanded.add(next.get(path)); });
    }

    function restore(json) {
        var previous = doc;
        doc = lib.TemplateDocument.parse(json);
        carryOverExpanded(previous, doc);
        committedJson = json;
        jsonDirty = false;
        editing = null;
        confirmingDelete = false;
        moving = false;
        ruleDraft = null;
        ruleErrors = {};
        confirmingRuleDelete = null;
        if (!doc.exists(selected)) selected = [];
        dirty = initialJson === null || json !== initialJson;
        updateStatus();
        renderTree();
        renderInspector();
        renderRules();
        if (isShown(panels.json)) {
            jsonArea.value = doc.toJson();
            clearJsonError();
        }
        updateHistoryButtons();
        renderMappingWarnings();
    }

    function undo() {
        if (jsonDirty && !applyJson()) return;
        commitHistory();
        if (!undoStack.length) return;
        redoStack.push(committedJson);
        restore(undoStack.pop());
        announce('Undone');
    }

    function redo() {
        if (jsonDirty && !applyJson()) return;
        commitHistory();
        if (!redoStack.length) return;
        undoStack.push(committedJson);
        restore(redoStack.pop());
        announce('Redone');
    }

    // A value changed: update the names shown in the structure in place so typing stays fast.
    function edited(path) {
        markDirty();
        var labels = treeLabels.get(lib.pathKey(path));
        if (labels) {
            labels.titles.forEach(function (element) { element.textContent = nodeTitle(path); });
            if (labels.type) labels.type.textContent = nodeType(path);
        }
        var heading = document.getElementById('tpl-inspector-title');
        if (heading && lib.samePath(path, selected)) heading.textContent = nodeTitle(path);
    }

    // The shape changed: rebuild the structure and the settings.
    function restructured() {
        markDirty();
        renderTree();
        renderInspector();
    }

    function select(path, focusHeading) {
        selected = path;
        editing = null;
        confirmingDelete = false;
        moving = false;
        for (var end = 2; end <= path.length; end++) {
            if (typeof path[end - 1] === 'number') expanded.add(doc.get(path.slice(0, end)));
        }
        renderTree();
        renderInspector();
        if (focusHeading) focusById('tpl-inspector-title');
    }

    function addAndSelect(arrayPath, node) {
        autoIds.add(node);
        var path = doc.insert(arrayPath, node);
        markDirty();
        select(path);
        var firstInput = inspector.querySelector('input[type="text"], textarea');
        if (firstInput) {
            firstInput.focus();
            firstInput.select();
        }
    }

    // ---- Structure (nested GOV.UK details) ---------------------------------------------------------------------

    function renderTree() {
        var targets = ruleTargets();
        treeLabels.clear();
        var rootTitle = h('span', { text: nodeTitle([]) });
        treeLabels.set('', { titles: [rootTitle] });

        tree.replaceChildren(h('div', null, [
            h('h2', { class: 'govuk-heading-s govuk-!-margin-bottom-1', text: 'Structure' }),
            h('p', { class: 'govuk-hint govuk-!-font-size-16', text: 'Drag sections, tasks, pages and questions to move them, or use the Move buttons.' }),
            h('p', { class: 'govuk-body govuk-!-font-size-16 govuk-!-margin-bottom-3' }, [
                editLink([], rootTitle, null),
                h('br'),
                h('span', { class: 'govuk-hint govuk-!-font-size-16', text: KIND_LABELS.template })
            ]),
            childrenOf([]).map(function (child) { return treeNode(child, targets); })
        ]));
    }

    function editLink(path, content, hiddenText) {
        var isCurrent = lib.samePath(path, selected);
        return h('a', {
            href: '#tpl-inspector-title',
            class: 'govuk-link' + (isCurrent ? ' govuk-!-font-weight-bold' : ''),
            'aria-current': isCurrent ? 'true' : null,
            onClick: function (e) {
                e.preventDefault();
                select(path, true);
            }
        }, [content, hidden(hiddenText)]);
    }

    function treeNode(path, targets) {
        var node = doc.get(path);
        var kind = doc.kindOf(path);
        var children = childrenOf(path);
        var summaryTitle = h('span', { text: nodeTitle(path) });
        var hiddenTitle = h('span', { text: nodeTitle(path) });
        var type = h('span', { text: nodeType(path) });
        treeLabels.set(lib.pathKey(path), { titles: [summaryTitle, hiddenTitle], type: type });

        var body;
        if (kind === 'page') {
            body = children.length
                ? h('ul', { class: 'govuk-list govuk-!-font-size-16 govuk-!-margin-bottom-0' }, children.map(function (child) {
                    return treeField(child, targets);
                }))
                : h('p', { class: 'govuk-hint govuk-!-font-size-16 govuk-!-margin-bottom-0', text: 'No questions yet' });
        } else {
            body = children.length
                ? children.map(function (child) { return treeNode(child, targets); })
                : h('p', { class: 'govuk-hint govuk-!-font-size-16 govuk-!-margin-bottom-0', text: 'Nothing added yet' });
        }

        var details = h('details', {
            class: 'govuk-details govuk-!-font-size-16 govuk-!-margin-bottom-2',
            open: expanded.has(node)
        }, [
            h('summary', { class: 'govuk-details__summary', draggable: 'true', 'data-tree-path': lib.pathKey(path) },
                h('span', { class: 'govuk-details__summary-text' + (lib.samePath(path, selected) ? ' govuk-!-font-weight-bold' : '') }, summaryTitle)),
            h('div', { class: 'govuk-details__text govuk-!-padding-top-1 govuk-!-padding-bottom-1' }, [
                h('p', { class: 'govuk-body govuk-!-font-size-16 govuk-!-margin-bottom-2' }, [
                    h('span', { class: 'govuk-hint govuk-!-font-size-16' }, type),
                    ' ',
                    editLink(path, [
                        'Edit',
                        h('span', { class: 'govuk-visually-hidden' }, [' ' + KIND_LABELS[kind].toLowerCase() + ' ', hiddenTitle])
                    ], null),
                    targets.has(idOf(path)) ? [' ', tag('Shown by a rule', 'yellow')] : null
                ]),
                body
            ])
        ]);
        details.addEventListener('toggle', function () {
            if (details.open) expanded.add(node); else expanded.delete(node);
        });
        return details;
    }

    function treeField(path, targets) {
        var title = h('span', { text: nodeTitle(path) });
        var type = h('span', { text: nodeType(path) });
        treeLabels.set(lib.pathKey(path), { titles: [title], type: type });
        return h('li', { draggable: 'true', 'data-tree-path': lib.pathKey(path) }, [
            editLink(path, title, null),
            h('br'),
            h('span', { class: 'govuk-hint govuk-!-font-size-16' }, type),
            targets.has(idOf(path)) ? [' ', tag('Shown by a rule', 'yellow')] : null
        ]);
    }

    // ---- Drag and drop in the structure ------------------------------------------------------------------------
    //
    // Mouse users can drag items. Everyone else uses Move up, Move down and Move to, which do the same thing.

    var dragSource = null;
    var dropMarker = null;

    // The list a dragged node of this kind would join if dropped on `target`, or null.
    function containerFor(kind, target) {
        var targetKind = doc.kindOf(target);
        var node = doc.get(target);
        if (kind === 'field' && targetKind === 'page') return target.concat(['fields']);
        if (kind === 'page' && targetKind === 'task' && Array.isArray(node.pages)) return target.concat(['pages']);
        if (kind === 'page' && (targetKind === 'flow' || targetKind === 'derivedFlow')) return target.concat(['pages']);
        if (kind === 'task' && targetKind === 'group') return target.concat(['tasks']);
        if (kind === 'flow' && targetKind === 'task' && doc.taskShape(node) === 'collection') return target.concat(['summary', 'flows']);
        if (kind === 'derivedFlow' && targetKind === 'task' && doc.taskShape(node) === 'derived') return target.concat(['summary', 'derivedFlows']);
        return null;
    }

    function dropFor(event) {
        if (!dragSource) return null;
        var element = event.target.closest && event.target.closest('[data-tree-path]');
        if (!element || !tree.contains(element)) return null;
        var target = lib.parsePathKey(element.getAttribute('data-tree-path'));
        if (lib.startsWith(target, dragSource)) return null;

        var kind = doc.kindOf(dragSource);
        if (doc.kindOf(target) === kind) {
            var box = element.getBoundingClientRect();
            var after = event.clientY > box.top + box.height / 2;
            return { element: element, arrayPath: target.slice(0, -1), index: target[target.length - 1] + (after ? 1 : 0), position: after ? 'after' : 'before' };
        }
        var into = containerFor(kind, target);
        return into ? { element: element, arrayPath: into, index: null, position: 'into' } : null;
    }

    function clearDropMarker() {
        if (!dropMarker) return;
        dropMarker.style.boxShadow = '';
        dropMarker.style.outline = '';
        dropMarker = null;
    }

    function showDropMarker(drop) {
        dropMarker = drop.element;
        if (drop.position === 'into') {
            dropMarker.style.outline = '3px solid var(--govuk-focus-colour, #ffdd00)';
        } else {
            dropMarker.style.boxShadow = 'inset 0 ' + (drop.position === 'before' ? '4px' : '-4px') + ' 0 0 var(--govuk-brand-colour, #1d70b8)';
        }
    }

    tree.addEventListener('dragstart', function (event) {
        var element = event.target.closest && event.target.closest('[data-tree-path]');
        if (!element) return;
        dragSource = lib.parsePathKey(element.getAttribute('data-tree-path'));
        event.dataTransfer.effectAllowed = 'move';
        event.dataTransfer.setData('text/plain', nodeTitle(dragSource));
    });

    tree.addEventListener('dragover', function (event) {
        var drop = dropFor(event);
        clearDropMarker();
        if (!drop) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = 'move';
        showDropMarker(drop);
    });

    tree.addEventListener('dragleave', function (event) {
        if (!tree.contains(event.relatedTarget)) clearDropMarker();
    });

    tree.addEventListener('drop', function (event) {
        var drop = dropFor(event);
        clearDropMarker();
        if (!drop) return;
        event.preventDefault();
        var newPath = doc.moveTo(dragSource, drop.arrayPath, drop.index);
        dragSource = null;
        markDirty();
        select(newPath);
        announce('Moved ' + nodeTitle(newPath));
    });

    tree.addEventListener('dragend', function () {
        dragSource = null;
        clearDropMarker();
    });

    // ---- Settings for the selected item ------------------------------------------------------------------------

    function renderInspector() {
        var focusId = inspector.contains(document.activeElement) ? document.activeElement.id : null;
        if (!doc.exists(selected)) selected = [];
        var builders = {
            template: templateInspector,
            group: groupInspector,
            task: taskInspector,
            page: pageInspector,
            field: fieldInspector,
            flow: flowInspector,
            derivedFlow: derivedFlowInspector
        };
        inspector.replaceChildren(h('div', null, builders[doc.kindOf(selected)](selected, doc.get(selected))));
        enhance(inspector);
        if (focusId) focusById(focusId);
    }

    function breadcrumbs(path) {
        var ancestors = ancestorsOf(path);
        if (!ancestors.length) return null;
        return h('nav', { class: 'govuk-breadcrumbs govuk-breadcrumbs--collapse-on-mobile govuk-!-margin-top-0', 'aria-label': 'Breadcrumb' },
            h('ol', { class: 'govuk-breadcrumbs__list' }, ancestors.map(function (ancestor) {
                return h('li', { class: 'govuk-breadcrumbs__list-item' }, h('a', {
                    href: '#tpl-inspector-title',
                    class: 'govuk-breadcrumbs__link',
                    onClick: function (e) {
                        e.preventDefault();
                        select(ancestor, true);
                    }
                }, nodeTitle(ancestor)));
            })));
    }

    function header(path) {
        return [
            breadcrumbs(path),
            h('span', { class: 'govuk-caption-m', text: KIND_LABELS[doc.kindOf(path)] }),
            h('h2', { class: 'govuk-heading-l', id: 'tpl-inspector-title', tabindex: '-1', text: nodeTitle(path) }),
            path.length ? nodeActions(path) : null,
            path.length && confirmingDelete ? deleteConfirmation(path) : null,
            path.length && moving ? movePanel(path) : null
        ];
    }

    var MOVE_TARGET_LABELS = { field: 'page', page: 'task or list', task: 'section', flow: 'task', derivedFlow: 'task' };

    // The lists a node could move to, grouped for a select, leaving out the one it's in.
    function moveDestinations(path) {
        var kind = doc.kindOf(path);
        var current = lib.pathKey(path.slice(0, -1));
        var items = [];
        function add(arrayPath, label, group) {
            var key = lib.pathKey(arrayPath);
            if (key !== current) items.push({ value: key, label: label, group: group });
        }
        if (kind === 'field') {
            doc.eachPageSet(function (pages, pagesPath, ownerPath) {
                var group = nodeTitle(ownerPath) + (doc.kindOf(ownerPath) === 'task' ? '' : ' (' + KIND_LABELS[doc.kindOf(ownerPath)].toLowerCase() + ')');
                pages.forEach(function (page, index) {
                    add(pagesPath.concat([index, 'fields']), page.title || page.pageId, group);
                });
            });
        } else if (kind === 'page') {
            (doc.root.taskGroups || []).forEach(function (group, g) {
                (group.tasks || []).forEach(function (task, t) {
                    var taskPath = ['taskGroups', g, 'tasks', t];
                    if (Array.isArray(task.pages)) add(taskPath.concat(['pages']), task.taskName || task.taskId, group.groupName || group.groupId);
                    var summary = task.summary || {};
                    ['flows', 'derivedFlows'].forEach(function (key) {
                        (summary[key] || []).forEach(function (flow, f) {
                            add(taskPath.concat(['summary', key, f, 'pages']), (flow.title || flow.flowId) + ' (in ' + (task.taskName || task.taskId) + ')', group.groupName || group.groupId);
                        });
                    });
                });
            });
        } else if (kind === 'task') {
            (doc.root.taskGroups || []).forEach(function (group, g) {
                add(['taskGroups', g, 'tasks'], group.groupName || group.groupId, 'Sections');
            });
        } else if (kind === 'flow' || kind === 'derivedFlow') {
            var shape = kind === 'flow' ? 'collection' : 'derived';
            doc.eachTask(function (task, taskPath) {
                if (doc.taskShape(task) === shape) add(taskPath.concat(['summary', kind === 'flow' ? 'flows' : 'derivedFlows']), task.taskName || task.taskId, 'Tasks');
            });
        }
        return items;
    }

    // A select whose options sit under headings that can't be chosen. Items without a group are listed on their own.
    function groupedSelect(options) {
        var entries = [];
        options.items.forEach(function (item) {
            if (!item.group) {
                entries.push({ item: item });
                return;
            }
            var group = entries.find(function (entry) { return entry.name === item.group; });
            if (!group) entries.push(group = { name: item.group, items: [] });
            group.items.push(item);
        });
        var option = function (item) { return h('option', { value: item.value, text: item.label }); };
        return formGroup([
            h('label', { class: 'govuk-label govuk-label--s', for: options.id, text: options.label }),
            hint(options.id, options.hint),
            h('select', {
                class: 'govuk-select',
                id: options.id,
                value: options.value == null ? undefined : options.value,
                'aria-describedby': describedBy(options.id, options.hint),
                onChange: options.onChange ? function (e) { options.onChange(e.target.value); } : null
            }, entries.map(function (entry) {
                return entry.item ? option(entry.item) : h('optgroup', { label: entry.name }, entry.items.map(option));
            }))
        ]);
    }

    // Where a question's answers are stored: at the top level, or inside a repeatable list.
    function listOwnerOf(arrayPath) {
        var owner = doc.ancestorPath(arrayPath.concat([0]), 'flow') || doc.ancestorPath(arrayPath.concat([0]), 'derivedFlow');
        return owner ? doc.get(owner).fieldId : '';
    }

    function movePanel(path) {
        var kind = doc.kindOf(path);
        var destinations = moveDestinations(path);
        var answered = inUse() && doc.fieldKeysIn(path).some(function (key) { return !!baselineFor(key); });
        return h('div', { id: 'tpl-move-panel' }, [
            groupedSelect({
                id: 'tpl-move-to',
                label: 'Move to which ' + MOVE_TARGET_LABELS[kind] + '?',
                hint: kind === 'field' ? 'It goes at the end of that page.' : 'It goes at the end.',
                items: destinations
            }),
            answered && (kind === 'field' || kind === 'page')
                ? inset('Applications have answers to questions here. Moving them into or out of a repeatable list changes where answers are saved, so check for problems after moving.')
                : null,
            h('div', { class: 'govuk-button-group' }, [
                button('Move', function () {
                    var destination = lib.parsePathKey(document.getElementById('tpl-move-to').value);
                    var crossesList = listOwnerOf(path.slice(0, -1)) !== listOwnerOf(destination);
                    var newPath = doc.moveTo(path, destination);
                    moving = false;
                    markDirty();
                    select(newPath, true);
                    announce('Moved to ' + nodeTitle(doc.parentPath(newPath)) + (crossesList && answered ? '. Check for problems before saving.' : ''));
                }, null, 'tpl-move-confirm'),
                h('a', {
                    href: '#tpl-inspector-title',
                    class: 'govuk-link',
                    onClick: function (e) {
                        e.preventDefault();
                        moving = false;
                        renderInspector();
                        focusById('tpl-move-to-button');
                    }
                }, 'Cancel')
            ])
        ]);
    }

    function nodeActions(path) {
        var index = path[path.length - 1];
        var siblings = doc.get(path.slice(0, -1));
        var canMoveElsewhere = MOVE_TARGET_LABELS[doc.kindOf(path)] && moveDestinations(path).length > 0;
        return h('div', { class: 'govuk-button-group' }, [
            index > 0 ? button('Move up', function () { selected = doc.move(path, -1); restructured(); }, 'secondary', 'tpl-move-up') : null,
            index < siblings.length - 1 ? button('Move down', function () { selected = doc.move(path, 1); restructured(); }, 'secondary', 'tpl-move-down') : null,
            canMoveElsewhere ? button('Move to another ' + MOVE_TARGET_LABELS[doc.kindOf(path)], function () {
                moving = true;
                confirmingDelete = false;
                renderInspector();
                focusById('tpl-move-to');
            }, 'secondary', 'tpl-move-to-button') : null,
            button('Duplicate', function () {
                var copy = doc.duplicate(path);
                markDirty();
                select(copy, true);
            }, 'secondary', 'tpl-duplicate'),
            button('Delete', function () {
                confirmingDelete = true;
                moving = false;
                renderInspector();
                focusById('tpl-delete-yes');
            }, 'warning', 'tpl-delete')
        ]);
    }

    function deleteConfirmation(path) {
        var kind = doc.kindOf(path);
        var kindLabel = KIND_LABELS[kind].toLowerCase();
        var message = 'Are you sure you want to delete this ' + kindLabel + (childrenOf(path).length ? ' and everything in it?' : '?');
        var references = doc.rulesReferencing(idOf(path)).length;
        if (references) message += ' ' + references + (references === 1 ? ' rule uses it and will need updating.' : ' rules use it and will need updating.');
        var mapped = mappingsReading(answerIdsIn(doc.get(path)));
        var mappingParts = mapped.length ? [
            h('p', { class: 'govuk-body', text: 'Event mappings read answers from here. After you save, update them in Event mappings or the events will be sent without these answers:' }),
            h('ul', { class: 'govuk-list govuk-list--bullet' }, mapped.map(function (reference) {
                return h('li', { text: '"' + reference.fieldId + '" in ' + describeMapping(reference) });
            }))
        ] : null;

        // Questions applications have already answered are retired, so reports keep their answers.
        var toRetire = inUse() ? doc.fieldKeysIn(path).filter(function (key) { return !!baselineFor(key); }) : [];
        var replacement = null;
        var retireParts = [];
        if (toRetire.length) {
            retireParts.push(h('p', { class: 'govuk-body', text: 'Applications already have answers to '
                + (toRetire.length === 1 ? 'this question' : 'these ' + toRetire.length + ' questions and lists')
                + '. Deleting retires ' + (toRetire.length === 1 ? 'it' : 'them') + ', so reports keep the answers:' }));
            retireParts.push(h('ul', { class: 'govuk-list govuk-list--bullet' }, toRetire.map(function (key) {
                return h('li', { text: key.fieldId + (key.parentFieldId ? ' (in ' + key.parentFieldId + ')' : '') });
            })));
            if (kind === 'field') {
                var others = [];
                doc.eachPageSet(function (pages) {
                    pages.forEach(function (page) {
                        (page.fields || []).forEach(function (field) {
                            if (field !== doc.get(path)) others.push({ value: field.fieldId, label: ((field.label && field.label.value) || field.fieldId) + ' (' + field.fieldId + ')' });
                        });
                    });
                });
                replacement = selectInput({
                    id: 'tpl-delete-replacement',
                    label: 'Does another question replace it?',
                    hint: 'Reports will show that the new question takes over from this one.',
                    value: '',
                    items: [{ value: '', label: 'No' }].concat(others),
                    onChange: function () { }
                });
            }
        }

        return h('div', { id: 'tpl-delete-confirm' }, [
            warningText(message),
            retireParts,
            mappingParts,
            replacement,
            h('div', { class: 'govuk-button-group' }, [
                button((toRetire.length ? 'Yes, retire and delete this ' : 'Yes, delete this ') + kindLabel, function () {
                    var replacedBy = document.getElementById('tpl-delete-replacement');
                    var parent = doc.parentPath(path);
                    doc.remove(path);
                    toRetire.forEach(function (key) {
                        doc.retire(key, replacedBy && replacedBy.value ? [replacedBy.value] : []);
                    });
                    markDirty();
                    select(parent, true);
                }, 'warning', 'tpl-delete-yes'),
                h('a', {
                    href: '#tpl-inspector-title',
                    class: 'govuk-link',
                    onClick: function (e) {
                        e.preventDefault();
                        confirmingDelete = false;
                        renderInspector();
                        focusById('tpl-delete');
                    }
                }, 'Cancel')
            ])
        ]);
    }

    // Name input that also regenerates the id while the item is new.
    function nameInput(path, options) {
        var node = doc.get(path);
        return textInput({
            id: options.id,
            label: options.label,
            hint: options.hint,
            value: options.get(node),
            onInput: function (value) {
                options.set(node, value);
                if (autoIds.has(node) && options.idKey) {
                    node[options.idKey] = doc.idFor(options.kind, value, node[options.idKey]);
                    if (options.kind === 'page') node.slug = lib.kebabCase(value) || node.pageId;
                    if (options.kind === 'flow' || options.kind === 'derivedFlow') node.fieldId = doc.idFor('field', value, node.fieldId);
                    [options.idKey, 'fieldId'].forEach(function (key) {
                        var idElement = document.getElementById('tpl-id-' + key);
                        if (idElement) idElement.value = node[key];
                    });
                }
                edited(path);
            }
        });
    }

    function bucketFor(path, key) {
        if (key === 'fieldId') return 'field';
        var kind = doc.kindOf(path);
        return kind === 'derivedFlow' ? 'flow' : kind;
    }

    function idInput(path, key, label, hintText) {
        var node = doc.get(path);
        var before = node[key];
        var message = h('p', { class: 'govuk-error-message', id: 'tpl-id-' + key + '-message', hidden: true });
        return textInput({
            id: 'tpl-id-' + key,
            label: label,
            hint: hintText,
            value: node[key],
            code: true,
            width: 20,
            describedBy: message.id,
            after: message,
            onFocus: function (value) { before = value; },
            onInput: function (value) {
                node[key] = value;
                autoIds.delete(node);
                edited(path);
            },
            onChange: function (value, input) {
                var trimmed = value.trim();
                var text = '';
                var isError = true;
                node[key] = '';
                var taken = trimmed && doc.collectIds()[bucketFor(path, key)].has(trimmed);
                node[key] = trimmed;
                if (!trimmed) {
                    text = 'Enter an id';
                } else if (taken) {
                    text = 'Something else in this template already uses this id';
                } else {
                    var notes = [];
                    var updated = 0;
                    if (doc.kindOf(path) === 'field' || doc.kindOf(path) === 'page') {
                        updated = doc.renameReferences(before, trimmed);
                    }
                    if (key === 'fieldId') {
                        var retired = answerIdRenamed(path, before, trimmed);
                        if (retired && retired.indexOf('already retired') !== -1) {
                            if (updated) doc.renameReferences(trimmed, before);
                            text = retired;
                            if (input) input.value = before;
                            node[key] = before;
                            trimmed = before;
                        } else {
                            if (updated) notes.push('Updated ' + updated + (updated === 1 ? ' rule' : ' rules') + ' to use the new id.');
                            if (retired) notes.push(retired);
                            var mapped = before && before !== trimmed ? mappingsReading([before]) : [];
                            if (mapped.length) {
                                notes.push(mapped.map(function (reference) { return describeMapping(reference).replace(/^t/, 'T'); }).join(', ')
                                    + ' still read' + (mapped.length === 1 ? 's' : '') + ' "' + before + '". Update ' + (mapped.length === 1 ? 'it' : 'them') + ' in Event mappings after you save.');
                            }
                            text = notes.join(' ');
                            isError = false;
                        }
                    } else {
                        if (updated) notes.push('Updated ' + updated + (updated === 1 ? ' rule' : ' rules') + ' to use the new id.');
                        text = notes.join(' ');
                        isError = false;
                    }
                }
                message.className = isError ? 'govuk-error-message' : 'govuk-hint';
                message.textContent = '';
                if (isError && text) append(message, h('span', { class: 'govuk-visually-hidden', text: 'Error: ' }));
                append(message, text);
                message.hidden = !text;
                message.parentElement.classList.toggle('govuk-form-group--error', isError && !!text);
                before = trimmed;
                edited(path);
            }
        });
    }

    // Once applications have answers saved under an id, renaming it is a retirement plus a replacement that keeps
    // the old reporting key. Renaming a replacement back undoes that. Reusing some other retired id is not allowed.
    function answerIdRenamed(path, before, after) {
        if (!before || before === after) return '';
        var node = doc.get(path);
        var parentFieldId = doc.fieldKeyOf(path).parentFieldId;
        var retired = doc.root.retiredFields || [];
        var retirementOfAfter = retired.find(function (entry) {
            return lib.fieldKey(entry.parentFieldId, entry.fieldId) === lib.fieldKey(parentFieldId, after);
        });
        var renamingBack = !!(retirementOfAfter && (retirementOfAfter.replacedBy || []).some(function (id) {
            return String(id).toLowerCase() === String(before).toLowerCase();
        }));

        if (retirementOfAfter && !renamingBack) {
            node.fieldId = before;
            return '"' + after + '" is already retired, choose another id';
        }

        (doc.root.retiredFields || []).forEach(function (entry) {
            if (entry.replacedBy) entry.replacedBy = entry.replacedBy.map(function (id) { return id === before ? after : id; });
        });

        if (renamingBack) {
            doc.unretire({ fieldId: after, parentFieldId: parentFieldId });
            var original = baselineFor({ fieldId: after, parentFieldId: parentFieldId });
            if (original && node.semanticKey === original.semanticKey) delete node.semanticKey;
            return '';
        }

        var previous = inUse() ? baselineFor({ fieldId: before, parentFieldId: parentFieldId }) : null;
        if (!previous) return '';
        doc.retire({ fieldId: before, parentFieldId: parentFieldId }, [after]);
        if (!node.semanticKey) node.semanticKey = previous.semanticKey;
        return 'Applications already have answers saved as "' + before + '", so it is retired and replaced by "' + after + '", which keeps its reporting key.';
    }

    function addForm(options) {
        var id = options.id;
        return section(options.title, [
            options.choices || null,
            textInput({ id: id, label: options.label, hint: options.hint }),
            button(options.button, function () {
                var input = document.getElementById(id);
                var name = (input.value || '').trim() || options.fallback;
                options.add(name);
            }, 'secondary', id + '-button')
        ]);
    }

    // A summary list of what's inside the selected item, each with a Change link that opens it.
    function contents(path, title, emptyText) {
        var targets = ruleTargets();
        var children = childrenOf(path);
        return section(title, children.length
            ? summaryList(children.map(function (child) {
                return {
                    key: [nodeTitle(child), targets.has(idOf(child)) ? [' ', tag('Shown by a rule', 'yellow')] : null],
                    value: nodeType(child),
                    actions: [{ text: 'Change', hidden: nodeTitle(child), onClick: function () { select(child, true); } }]
                };
            }))
            : h('p', { class: 'govuk-body', text: emptyText }));
    }

    function isInList(path) {
        return !!(doc.ancestorPath(path, 'flow') || doc.ancestorPath(path, 'derivedFlow'));
    }

    function ruleList(path) {
        var id = idOf(path);
        var kindLabel = KIND_LABELS[doc.kindOf(path)].toLowerCase();
        var rules = doc.rulesReferencing(id);
        var taskPath = doc.ancestorPath(path, 'task');
        return section('Rules', [
            rules.length
                ? h('ul', { class: 'govuk-list govuk-list--bullet' }, rules.map(function (rule) {
                    return h('li', { text: (rule.enabled === false ? '(Turned off) ' : '') + 'When ' + describeConditions(rule.conditionGroup) + ': ' + describeActions(rule) + '.' });
                }))
                : h('p', { class: 'govuk-body', text: 'No rules use this ' + kindLabel + '. Applicants always see it.' }),
            isInList(path) || !taskPath ? null : button('Add a rule that shows this ' + kindLabel, function () {
                startRule({
                    pages: doc.kindOf(path) === 'page' ? [id] : [],
                    fields: doc.kindOf(path) === 'field' ? [id] : [],
                    targetTaskId: doc.get(taskPath).taskId
                });
            }, 'secondary', 'tpl-add-rule-here')
        ]);
    }

    var SYSTEM_COLUMNS = [
        { value: 'reference', label: 'Reference' },
        { value: 'dateStarted', label: 'Date started' },
        { value: 'dateSubmitted', label: 'Date submitted' },
        { value: 'status', label: 'Status' },
        { value: 'action', label: 'Action' }
    ];

    var APPLICATION_BINDINGS = [
        { value: 'application.reference', label: 'Application reference' },
        { value: 'application.version', label: 'Template version the application uses' },
        { value: 'application.leadApplicant', label: 'Lead applicant\'s name' },
        { value: 'application.leadApplicantEmail', label: 'Lead applicant\'s email' }
    ];

    // Every answer a dashboard column or task list detail can show, grouped by task and by the list each question is in,
    // in the order applicants see them. A whole list isn't a choice: it would show as raw data.
    function answerChoices() {
        var choices = [];
        function addQuestions(pages, group) {
            (pages || []).forEach(function (page) {
                (page.fields || []).forEach(function (field) {
                    if (field.fieldId) choices.push({ value: field.fieldId, label: ((field.label && field.label.value) || field.fieldId) + ' (' + field.fieldId + ')', group: group });
                });
            });
        }
        doc.eachTask(function (task) {
            var taskName = task.taskName || task.taskId;
            var summary = task.summary || {};
            addQuestions(task.pages, taskName);
            (summary.flows || []).forEach(function (flow) {
                addQuestions(flow.pages, taskName + ': ' + (flow.title || flow.fieldId) + ' (repeatable list)');
            });
            (summary.derivedFlows || []).forEach(function (flow) {
                addQuestions(flow.pages, taskName + ': ' + (flow.title || flow.fieldId) + ' (repeated pages)');
            });
        });
        return choices;
    }

    function listTitleFor(fieldId) {
        var found = null;
        doc.eachTask(function (task) {
            var summary = task.summary || {};
            (summary.flows || []).concat(summary.derivedFlows || []).forEach(function (flow) {
                if (!found && fieldId && flow.fieldId === fieldId) found = flow.title || flow.fieldId;
            });
        });
        return found;
    }

    function wholeListWarning(fieldId) {
        var title = listTitleFor(fieldId);
        return title
            ? warningText('This shows the whole list "' + title + '", which appears as raw data. Choose one of the questions in the list instead.')
            : null;
    }

    function choiceLabel(choices, value) {
        var match = choices.find(function (choice) { return choice.value === value; });
        return match ? match.label : value;
    }

    // Shared list editing: a summary list with Change, Move and Remove, and a form for the item being changed.
    function listEditor(options) {
        var items = options.items;
        function moved() {
            if (options.renumber) options.renumber();
            editing = null;
            restructured();
        }
        var rows = items.map(function (item, index) {
            var name = options.noun + ' ' + (index + 1);
            return {
                key: options.key(item, index),
                value: options.value(item, index),
                actions: [
                    { text: 'Change', hidden: name, onClick: function () { startEditing(options.editKey, index, options.firstInputId); } },
                    index > 0 ? { text: 'Move up', hidden: name, onClick: function () { items.splice(index - 1, 0, items.splice(index, 1)[0]); moved(); } } : null,
                    index < items.length - 1 ? { text: 'Move down', hidden: name, onClick: function () { items.splice(index + 1, 0, items.splice(index, 1)[0]); moved(); } } : null,
                    { text: 'Remove', hidden: name, onClick: function () { items.splice(index, 1); moved(); } }
                ]
            };
        });
        var parts = [items.length ? summaryList(rows) : h('p', { class: 'govuk-body', text: options.emptyText })];
        if (isEditing(options.editKey) && items[editing.index]) {
            parts.push(h('fieldset', { class: 'govuk-fieldset' }, [
                h('legend', { class: 'govuk-fieldset__legend govuk-fieldset__legend--m', text: options.noun.charAt(0).toUpperCase() + options.noun.slice(1) + ' ' + (editing.index + 1) }),
                options.form(items[editing.index], editing.index),
                button('Done', function () { stopEditing(options.addId); }, 'secondary', options.editKey + '-done')
            ]));
        } else if (!options.max || items.length < options.max) {
            parts.push(button(options.addText, function () {
                items.push(options.create());
                if (options.renumber) options.renumber();
                markDirty();
                startEditing(options.editKey, items.length - 1, options.firstInputId);
            }, 'secondary', options.addId));
        }
        return parts;
    }

    function isFieldColumn(column) {
        return column.type === 'field' || (!column.type && !!column.fieldId);
    }

    function dashboardEditor() {
        var root = doc.root;
        var columns = root.dashboard && Array.isArray(root.dashboard.columns) ? root.dashboard.columns : null;
        if (!columns || !columns.length) {
            return section('Dashboard columns', [
                h('p', { class: 'govuk-body', text: 'The applications dashboard shows the default columns: reference, date started, date submitted, status and an action link.' }),
                button('Choose the columns', function () {
                    root.dashboard = { columns: SYSTEM_COLUMNS.map(function (column, index) { return { type: 'system', id: column.value, order: (index + 1) * 10 }; }) };
                    restructured();
                }, 'secondary', 'tpl-dashboard-customise')
            ]);
        }

        var choices = answerChoices();
        var answerColumns = columns.filter(isFieldColumn).length;
        return section('Dashboard columns', [
            h('p', { class: 'govuk-body', text: 'Columns on the applications dashboard, in order. Up to 3 can show answers.' }),
            answerColumns > 3 ? warningText('Only the first 3 answer columns are shown.') : null,
            listEditor({
                items: columns,
                noun: 'column',
                editKey: 'column',
                firstInputId: 'tpl-column-type-0',
                addId: 'tpl-add-column',
                addText: 'Add a column',
                emptyText: 'There are no columns.',
                renumber: function () { columns.forEach(function (column, index) { column.order = (index + 1) * 10; }); },
                key: function (column) {
                    return column.header || (isFieldColumn(column) ? column.fieldId || 'Answer not chosen' : choiceLabel(SYSTEM_COLUMNS, column.id));
                },
                value: function (column) {
                    if (!isFieldColumn(column)) return 'Built in: ' + choiceLabel(SYSTEM_COLUMNS, column.id);
                    if (listTitleFor(column.fieldId)) return 'The whole list "' + listTitleFor(column.fieldId) + '": choose a question in it';
                    return 'Answer: ' + (column.fieldId ? choiceLabel(choices, column.fieldId) : 'not chosen');
                },
                create: function () { return { type: 'field', fieldId: '', header: '', order: 0 }; },
                form: function (column) {
                    var showsAnswer = isFieldColumn(column);
                    return [
                        radios({
                            name: 'tpl-column-type', legend: 'What does the column show?', inline: true, value: showsAnswer ? 'field' : 'system',
                            items: [{ value: 'field', label: 'An answer' }, { value: 'system', label: 'Something built in' }],
                            onChange: function (value) {
                                column.type = value;
                                if (value === 'system') {
                                    delete column.fieldId;
                                    column.id = column.id || 'reference';
                                } else {
                                    delete column.id;
                                    column.fieldId = column.fieldId || '';
                                }
                                markDirty();
                                renderInspector();
                            }
                        }),
                        showsAnswer && listTitleFor(column.fieldId) ? wholeListWarning(column.fieldId) : null,
                        showsAnswer
                            ? groupedSelect({
                                id: 'tpl-column-field', label: 'Answer', value: listTitleFor(column.fieldId) ? '' : column.fieldId,
                                hint: 'Questions are grouped by task, and by the repeatable list they are in.',
                                items: [{ value: '', label: 'Choose an answer' }].concat(choices),
                                onChange: function (value) { column.fieldId = value; markDirty(); renderInspector(); }
                            })
                            : selectInput({
                                id: 'tpl-column-system', label: 'Built-in column', value: column.id, items: SYSTEM_COLUMNS,
                                onChange: function (value) { column.id = value; markDirty(); }
                            }),
                        textInput({
                            id: 'tpl-column-header', label: showsAnswer ? 'Column heading' : 'Column heading (optional)', value: column.header, width: 20,
                            onInput: function (value) { column.header = value || undefined; if (!value) delete column.header; markDirty(); }
                        })
                    ];
                }
            }),
            h('p', { class: 'govuk-body' }, actionLink({
                text: 'Use the default columns',
                onClick: function () { root.dashboard = null; editing = null; restructured(); }
            }))
        ]);
    }

    function metadataEditor() {
        var root = doc.root;
        var items = root.applicationMetaData && Array.isArray(root.applicationMetaData.items) ? root.applicationMetaData.items : null;
        if (!items || !items.length) {
            return section('Details on the task list', [
                h('p', { class: 'govuk-body', text: 'The task list shows the default details: application reference, template version and lead applicant.' }),
                button('Choose the details', function () {
                    root.applicationMetaData = {
                        items: [
                            { label: 'Application reference', fieldId: 'application.reference', order: 10 },
                            { label: 'Application version', fieldId: 'application.version', order: 20 },
                            { label: 'Lead applicant', fieldId: 'application.leadApplicant', order: 30 }
                        ]
                    };
                    restructured();
                }, 'secondary', 'tpl-metadata-customise')
            ]);
        }

        var choices = APPLICATION_BINDINGS.map(function (binding) { return Object.assign({ group: 'About the application' }, binding); })
            .concat(answerChoices());
        return section('Details on the task list', [
            h('p', { class: 'govuk-body', text: 'Rows shown under "Your application" on the task list, in order. You can have up to 10.' }),
            listEditor({
                items: items,
                noun: 'detail',
                editKey: 'detail',
                firstInputId: 'tpl-detail-label',
                addId: 'tpl-add-detail',
                addText: 'Add a detail',
                emptyText: 'There are no details.',
                max: 10,
                renumber: function () { items.forEach(function (item, index) { item.order = (index + 1) * 10; }); },
                key: function (item) { return item.label || 'No label'; },
                value: function (item) {
                    if (listTitleFor(item.fieldId)) return 'The whole list "' + listTitleFor(item.fieldId) + '": choose a question in it';
                    return item.fieldId ? choiceLabel(choices, item.fieldId) : 'Not chosen';
                },
                create: function () { return { label: '', fieldId: '', order: 0 }; },
                form: function (item) {
                    var wholeList = !!listTitleFor(item.fieldId);
                    var isCustom = !wholeList && ((editing && editing.custom)
                        || (!!item.fieldId && !choices.some(function (choice) { return choice.value === item.fieldId; })));
                    return [
                        textInput({
                            id: 'tpl-detail-label', label: 'Label', value: item.label,
                            onInput: function (value) { item.label = value; markDirty(); }
                        }),
                        wholeList ? wholeListWarning(item.fieldId) : null,
                        groupedSelect({
                            id: 'tpl-detail-field', label: 'What it shows', value: isCustom ? 'custom' : wholeList ? '' : item.fieldId,
                            hint: 'Questions are grouped by task, and by the repeatable list they are in.',
                            items: [{ value: '', label: 'Choose something to show' }].concat(choices, [{ value: 'custom', label: 'Something else (enter a path)' }]),
                            onChange: function (value) {
                                editing.custom = value === 'custom';
                                if (!editing.custom) item.fieldId = value;
                                markDirty();
                                renderInspector();
                            }
                        }),
                        isCustom
                            ? textInput({
                                id: 'tpl-detail-path', label: 'Answer path', value: item.fieldId,
                                hint: 'For an answer in a list, name the list and the question, for example trusts[0].trustSearch.name for the first trust\'s name.',
                                onInput: function (value) { item.fieldId = value; markDirty(); }
                            })
                            : null
                    ];
                }
            }),
            h('p', { class: 'govuk-body' }, actionLink({
                text: 'Use the default details',
                onClick: function () { root.applicationMetaData = null; editing = null; restructured(); }
            }))
        ]);
    }

    function retiredList() {
        var retired = doc.root.retiredFields || [];
        if (!retired.length) return null;
        return section('Retired questions', [
            h('p', { class: 'govuk-body', text: 'Questions taken out of the template after applications had answered them. Reports keep their answers.' }),
            summaryList(retired.map(function (entry) {
                return {
                    key: entry.fieldId + (entry.parentFieldId ? ' (in ' + entry.parentFieldId + ')' : ''),
                    value: entry.replacedBy && entry.replacedBy.length ? 'Replaced by ' + entry.replacedBy.join(', ') : 'Not replaced'
                };
            }))
        ]);
    }

    function templateInspector(path) {
        var root = doc.root;

        return [
            header(path),
            textInput({
                id: 'tpl-template-name', label: 'Template name', value: root.templateName,
                onInput: function (value) { root.templateName = value; edited(path); }
            }),
            textArea({
                id: 'tpl-template-description', label: 'Description', value: root.description, rows: 2,
                onInput: function (value) { root.description = value; edited(path); }
            }),
            textInput({
                id: 'tpl-id-templateId', label: 'Template id', value: root.templateId, width: 20,
                hint: 'An id inside the JSON, not the database id. Keep it the same across versions.',
                onInput: function (value) { root.templateId = value; edited(path); }
            }),
            radios({
                name: 'tpl-requirement-policy',
                legend: 'Do applicants have to answer questions unless you say otherwise?',
                value: root.defaultFieldRequirementPolicy === 'required' ? 'required' : 'optional',
                items: [
                    { value: 'required', label: 'Yes, questions are required by default' },
                    { value: 'optional', label: 'No, questions are optional by default' }
                ],
                onChange: function (value) { root.defaultFieldRequirementPolicy = value; restructured(); }
            }),
            checkbox({
                id: 'tpl-hide-single-label',
                label: 'Use the page heading as the question when a page has one question',
                hint: 'The question label is hidden so it isn\'t repeated under the heading.',
                checked: root.hideFieldLabelWhenOnlyOneField !== false,
                onChange: function (checked) { root.hideFieldLabelWhenOnlyOneField = checked; edited(path); }
            }),
            checkbox({
                id: 'tpl-contributors',
                label: 'Let applicants invite contributors',
                checked: root.contributorPattern !== false,
                onChange: function (checked) { root.contributorPattern = checked; edited(path); }
            }),
            contents(path, 'Sections', 'This template has no sections yet.'),
            addForm({
                id: 'tpl-new-group',
                title: 'Add a section',
                label: 'Section heading',
                hint: 'Sections group tasks on the task list.',
                button: 'Add section',
                fallback: 'New section',
                add: function (name) { addAndSelect(['taskGroups'], doc.newGroup(name)); }
            }),
            dashboardEditor(),
            metadataEditor(),
            retiredList()
        ];
    }

    function groupInspector(path, group) {
        return [
            header(path),
            nameInput(path, {
                id: 'tpl-group-name', label: 'Section heading', kind: 'group', idKey: 'groupId',
                get: function (node) { return node.groupName; },
                set: function (node, value) { node.groupName = value; }
            }),
            idInput(path, 'groupId', 'Section id', null),
            contents(path, 'Tasks', 'Add at least one task. A section without tasks can\'t be saved.'),
            addForm({
                id: 'tpl-new-task',
                title: 'Add a task',
                label: 'Task name',
                button: 'Add task',
                fallback: 'New task',
                choices: radios({
                    name: 'tpl-new-task-shape',
                    legend: 'What kind of task?',
                    value: 'pages',
                    items: TASK_SHAPES,
                    onChange: function () { }
                }),
                add: function (name) {
                    var shape = (inspector.querySelector('input[name="tpl-new-task-shape"]:checked') || {}).value;
                    addAndSelect(path.concat(['tasks']), doc.newTask(name, shape));
                }
            })
        ];
    }

    var TASK_SHAPES = [
        { value: 'pages', label: 'Questions on one or more pages' },
        { value: 'collection', label: 'A repeatable list', hint: 'Applicants add as many items as they need, for example trusts or academies.' },
        { value: 'derived', label: 'Pages repeated for each item in another list', hint: 'For example a declaration for each trust added earlier.' }
    ];

    function taskTypeSetting(path, task) {
        var shape = doc.taskShape(task);
        if (!doc.isTaskEmpty(task)) {
            return summaryList([{
                key: 'Task type',
                value: [TASK_SHAPES.find(function (item) { return item.value === shape; }).label, h('br'),
                    h('span', { class: 'govuk-hint', text: 'To change the type, delete its pages first.' })]
            }], true);
        }
        return radios({
            name: 'tpl-task-shape', legend: 'Task type', value: shape, items: TASK_SHAPES,
            onChange: function (value) {
                doc.setTaskShape(task, value);
                restructured();
            }
        });
    }

    function taskInspector(path, task) {
        var mode = task.summary && task.summary.mode;
        var items = [
            header(path),
            nameInput(path, {
                id: 'tpl-task-name', label: 'Task name', kind: 'task', idKey: 'taskId',
                get: function (node) { return node.taskName; },
                set: function (node, value) { node.taskName = value; }
            }),
            textInput({
                id: 'tpl-task-caption', label: 'Caption (optional)', value: task.caption,
                hint: 'Shown above each page heading. Include answers like this: {trustSearch.name ?? Trust details}',
                onInput: function (value) { task.caption = value || null; edited(path); }
            }),
            idInput(path, 'taskId', 'Task id', 'Used in the page address.'),
            checkbox({
                id: 'tpl-task-visible', label: 'Show this task on the task list', checked: task.visibleInTaskList !== false,
                onChange: function (checked) { task.visibleInTaskList = checked; edited(path); }
            }),
            taskTypeSetting(path, task)
        ];

        if (mode === 'multiCollectionFlow') {
            items.push(contents(path, 'Repeatable lists', 'This task has no lists yet.'));
            items.push(addForm({
                id: 'tpl-new-flow', title: 'Add a repeatable list', label: 'List name', button: 'Add list', fallback: 'New list',
                add: function (name) { addAndSelect(path.concat(['summary', 'flows']), doc.newFlow(name)); }
            }));
        } else if (mode === 'derivedCollectionFlow') {
            items.push(textInput({
                id: 'tpl-task-summary-title', label: 'Heading on the task page', value: task.summary.title,
                onInput: function (value) { task.summary.title = value || null; edited(path); }
            }));
            items.push(textArea({
                id: 'tpl-task-summary-description', label: 'Text under the heading (optional)', value: task.summary.description, rows: 2,
                onInput: function (value) { task.summary.description = value || null; edited(path); }
            }));
            items.push(contents(path, 'Repeated pages', 'This task has nothing to repeat yet.'));
            items.push(addForm({
                id: 'tpl-new-derived', title: 'Repeat pages for another list', label: 'Name', button: 'Add', fallback: 'New repeated pages',
                add: function (name) {
                    if (!Array.isArray(task.summary.derivedFlows)) task.summary.derivedFlows = [];
                    addAndSelect(path.concat(['summary', 'derivedFlows']), doc.newDerivedFlow(name));
                }
            }));
        } else {
            items.push(checkbox({
                id: 'tpl-task-start-first', label: 'Open the first page when the task hasn\'t been started',
                hint: 'Otherwise applicants see the task summary first.',
                checked: task.startAtFirstPageWhenNotStarted === true,
                onChange: function (checked) { task.startAtFirstPageWhenNotStarted = checked || null; edited(path); }
            }));
            items.push(contents(path, 'Pages', 'This task has no pages yet.'));
            items.push(addForm({
                id: 'tpl-new-page', title: 'Add a page', label: 'Page heading', button: 'Add page', fallback: 'New page',
                hint: 'Each page is one screen. Add questions to it next.',
                add: function (name) { addAndSelect(path.concat(['pages']), doc.newPage(name)); }
            }));
        }
        return items;
    }

    function pageInspector(path, page) {
        var defaultNavigation = page.returnToSummaryPage === false ? 'Go to the next page (default)' : 'Go back to the task summary (default)';
        return [
            header(path),
            nameInput(path, {
                id: 'tpl-page-title', label: 'Page heading', kind: 'page', idKey: 'pageId',
                hint: 'On a page with one question, the heading is the question.',
                get: function (node) { return node.title; },
                set: function (node, value) { node.title = value; }
            }),
            textArea({
                id: 'tpl-page-description', label: 'Text under the heading (optional)', value: page.description,
                hint: 'You can use Markdown, for example **bold** or a list.',
                onInput: function (value) { page.description = value; edited(path); }
            }),
            idInput(path, 'pageId', 'Page id', 'Used in the page address and by rules.'),
            radios({
                name: 'tpl-page-navigation',
                legend: 'After the applicant saves this page',
                value: page.navigationAfterSave || '',
                items: [
                    { value: '', label: defaultNavigation },
                    { value: 'linear', label: 'Go to the next page' },
                    { value: 'summary', label: 'Go back to the task summary' },
                    { value: 'branch', label: 'Go to the next page a rule shows', hint: 'Use this with rules that show different pages depending on the answer.' }
                ],
                onChange: function (value) { doc.set(path, 'navigationAfterSave', value || undefined); edited(path); }
            }),
            textInput({
                id: 'tpl-page-button', label: 'Button text (optional)', value: page.saveButtonLabel, width: 20,
                hint: 'Leave blank for "Save and continue".',
                onInput: function (value) { page.saveButtonLabel = value || null; edited(path); }
            }),
            contents(path, 'Questions', 'This page has no questions yet.'),
            section('Add a question', [
                selectInput({
                    id: 'tpl-new-field-type', label: 'Question type', value: 'text',
                    items: FIELD_TYPES, onChange: function () { }
                }),
                textInput({
                    id: 'tpl-new-field-label', label: 'Question',
                    hint: (page.fields || []).length ? null : 'Leave blank to use the page heading.'
                }),
                button('Add question', function () {
                    var type = document.getElementById('tpl-new-field-type').value;
                    var label = document.getElementById('tpl-new-field-label').value.trim()
                        || ((page.fields || []).length ? 'New question' : page.title || 'New question');
                    var field = doc.newField('text', label);
                    changeFieldType(field, type);
                    setRequired(field, 'required');
                    addAndSelect(path.concat(['fields']), field);
                }, 'secondary', 'tpl-new-field-button')
            ]),
            ruleList(path)
        ];
    }

    // A question applications have answered can't start storing a different kind of answer under the same id.
    function kindChangeWarning(path, field) {
        if (!inUse()) return null;
        var key = doc.fieldKeyOf(path);
        var previous = baselineFor(key);
        if (!previous && field.semanticKey) {
            // A renamed question that kept an earlier question's reporting key.
            var takenOver = Array.from(baselineFields.values()).find(function (item) {
                return item.parentFieldId.toLowerCase() === key.parentFieldId.toLowerCase()
                    && item.semanticKey.toLowerCase() === field.semanticKey.toLowerCase();
            });
            if (!takenOver || takenOver.kind === lib.fieldKind(field)) return null;
            return h('div', null, [
                warningText('This question keeps the reporting key of "' + takenOver.fieldId + '", which stored ' + takenOver.kind
                    + '. It now stores ' + lib.fieldKind(field) + ', so it needs its own reporting key.'),
                button('Give it its own reporting key', function () {
                    delete field.semanticKey;
                    restructured();
                }, 'secondary', 'tpl-own-reporting-key')
            ]);
        }
        if (!previous || previous.kind === lib.fieldKind(field)) return null;
        return h('div', null, [
            warningText('Applications already have answers to this question saved as ' + previous.kind + '. It now stores '
                + lib.fieldKind(field) + ', so it needs a new answer id. The old one will be retired.'),
            button('Give it a new answer id', function () {
                var newId = doc.idFor('field', (field.label && field.label.value) || field.fieldId);
                doc.retire(key, [newId]);
                doc.renameReferences(field.fieldId, newId);
                field.fieldId = newId;
                delete field.semanticKey;
                restructured();
            }, 'secondary', 'tpl-new-answer-id')
        ]);
    }

    function reportingKeySetting(path, field) {
        var details = h('details', { class: 'govuk-details' }, [
            h('summary', { class: 'govuk-details__summary' }, h('span', { class: 'govuk-details__summary-text', text: 'Reporting key (advanced)' })),
            h('div', { class: 'govuk-details__text' }, textInput({
                id: 'tpl-field-semantic-key', label: 'Reporting key', value: field.semanticKey, width: 20,
                hint: 'Reports use this to line answers up across versions. It\'s the answer id unless you set it. It can\'t contain "/".',
                onInput: function (value) {
                    if (value.trim()) field.semanticKey = value.trim(); else delete field.semanticKey;
                    edited(path);
                }
            }))
        ]);
        if (field.semanticKey) details.setAttribute('open', '');
        return details;
    }

    // "Only check this when …" for length limits and format checks. Required stays unconditional, because task
    // completion ignores conditions on required checks.
    function conditionSetting(path, rule, idPrefix) {
        var condition = rule.condition;
        var parts = [checkbox({
            id: idPrefix + '-conditional',
            label: 'Only check this when another answer matches',
            checked: !!condition,
            onChange: function (checked) {
                if (checked) rule.condition = { triggerField: '', operator: 'equals', value: '', dataType: 'string' };
                else delete rule.condition;
                markDirty();
                renderInspector();
            }
        })];
        if (condition) {
            var trigger = questionById(condition.triggerField);
            parts.push(h('div', { class: 'govuk-inset-text' }, [
                questionSelect({
                    id: idPrefix + '-trigger', label: 'Question', value: condition.triggerField,
                    onChange: function (value) { condition.triggerField = value; condition.value = ''; markDirty(); renderInspector(); }
                }),
                radios({
                    name: idPrefix + '-operator', legend: 'Check when the answer', inline: true, value: condition.operator,
                    items: [{ value: 'equals', label: 'is' }, { value: 'notEquals', label: 'is not' }],
                    onChange: function (value) { condition.operator = value; markDirty(); }
                }),
                answerValueInput(idPrefix + '-value', 'Answer', trigger, condition.value, function (value) { condition.value = value; markDirty(); })
            ]));
        }
        return parts;
    }

    function conditionText(condition) {
        if (!condition) return null;
        var operator = condition.operator === 'notEquals' ? 'is not' : 'is';
        return 'Only when ' + titleForId(condition.triggerField) + ' ' + operator + ' "' + condition.value + '"';
    }

    function fieldInspector(path, field) {
        var type = editorType(field);
        var label = field.label || (field.label = { value: '', isVisible: false });
        var requiredRule = findRule(field, ['required']);
        var policy = doc.root.defaultFieldRequirementPolicy === 'required' ? 'required' : 'optional';

        var requiredMessage = textInput({
            id: 'tpl-field-required-message',
            label: 'Error message if it isn\'t answered',
            value: requiredRule ? requiredRule.message : defaultRequiredMessage(field),
            onInput: function (value) {
                var rule = findRule(field, ['required']);
                if (rule) rule.message = value;
                edited(path);
            }
        });

        var items = [
            header(path),
            selectInput({
                id: 'tpl-field-type', label: 'Question type', value: type, items: FIELD_TYPES,
                onChange: function (value) { changeFieldType(field, value); restructured(); }
            }),
            kindChangeWarning(path, field),
            nameInput(path, {
                id: 'tpl-field-label', label: 'Question', kind: 'field', idKey: 'fieldId',
                get: function (node) { return node.label.value; },
                set: function (node, value) {
                    var rule = findRule(node, ['required']);
                    var followsLabel = rule && rule.message === defaultRequiredMessage(node);
                    node.label.value = value;
                    if (followsLabel) {
                        rule.message = defaultRequiredMessage(node);
                        document.getElementById('tpl-field-required-message').value = rule.message;
                    }
                }
            }),
            checkbox({
                id: 'tpl-field-label-visible', label: 'Show the question above the answer',
                hint: 'Turn this off when the page heading already asks the question.',
                checked: label.isVisible === true,
                onChange: function (checked) { label.isVisible = checked; edited(path); }
            }),
            textArea({
                id: 'tpl-field-hint', label: 'Hint (optional)', value: field.tooltip, rows: 2,
                hint: 'Help text shown under the question. You can use Markdown.',
                onInput: function (value) { field.tooltip = value || null; edited(path); }
            }),
            idInput(path, 'fieldId', 'Answer id', inUse()
                ? 'Answers are saved against this id. If you change it, the old id is retired and the new one keeps its reporting key.'
                : 'Answers are saved against this id.'),
            reportingKeySetting(path, field),
            radios({
                name: 'tpl-field-required',
                legend: 'Do applicants have to answer this?',
                value: requiredState(field),
                items: [
                    { value: 'required', label: 'Yes', conditional: requiredMessage },
                    { value: 'optional', label: 'No' },
                    { value: 'default', label: 'Use the template setting (' + policy + ')' }
                ],
                onChange: function (value) {
                    setRequired(field, value, document.getElementById('tpl-field-required-message').value);
                    edited(path);
                }
            })
        ];

        if (lib.isChoiceType(field.type)) items.push(optionsEditor(path, field));
        if (field.type === 'date') {
            items.push(textInput({
                id: 'tpl-field-date-name', label: 'Name used in error messages', value: label.validationLabelValue,
                hint: 'For example "Date signed" gives "Date signed must include a month".',
                onInput: function (value) { label.validationLabelValue = value || null; edited(path); }
            }));
        }
        if (field.type === 'complexField') items.push(complexFieldEditor(path, field, type));
        if (TEXT_TYPES.indexOf(field.type) !== -1) items.push(lengthEditor(path, field));
        if (TEXT_TYPES.indexOf(field.type) !== -1 || type === 'search') items.push(patternEditor(path, field));

        var other = (field.validations || []).filter(function (rule) {
            return (rule.type === 'required' && rule.condition)
                || ['required', 'regex', 'maxLength', 'maxWords'].indexOf(rule.type) === -1
                || (TEXT_TYPES.indexOf(field.type) === -1 && (rule.type === 'maxLength' || rule.type === 'maxWords'));
        });
        if (other.length) items.push(inset(other.length + (other.length === 1 ? ' more check on this question is' : ' more checks on this question are') + ' set in the JSON tab.'));

        items.push(ruleList(path));
        return items;
    }

    function startEditing(key, index, focusId) {
        editing = { key: key, index: index };
        renderInspector();
        focusById(focusId);
    }

    function stopEditing(focusId) {
        editing = null;
        renderInspector();
        if (focusId) focusById(focusId);
    }

    function isEditing(key, index) {
        return !!editing && editing.key === key && (index === undefined || editing.index === index);
    }

    function optionsEditor(path, field) {
        var options = field.options || (field.options = []);

        function move(from, to) {
            options.splice(to, 0, options.splice(from, 1)[0]);
            editing = null;
            restructured();
        }

        var rows = options.map(function (option, index) {
            var name = 'option ' + (index + 1);
            return {
                key: option.label || 'No text',
                value: option.value,
                actions: [
                    { text: 'Change', hidden: name, onClick: function () { startEditing('option', index, 'tpl-option-text'); } },
                    index > 0 ? { text: 'Move up', hidden: name, onClick: function () { move(index, index - 1); } } : null,
                    index < options.length - 1 ? { text: 'Move down', hidden: name, onClick: function () { move(index, index + 1); } } : null,
                    { text: 'Remove', hidden: name, onClick: function () { options.splice(index, 1); editing = null; restructured(); } }
                ]
            };
        });

        var parts = [
            h('p', { class: 'govuk-body', text: 'Applicants see the text. The saved value is stored with the answer and used by rules.' }),
            options.length ? summaryList(rows) : h('p', { class: 'govuk-body', text: 'There are no options yet.' })
        ];

        if (isEditing('option') && options[editing.index]) {
            parts.push(optionForm(path, options[editing.index], editing.index));
        } else {
            parts.push(button('Add an option', function () {
                var number = options.length + 1;
                var option = { value: 'option-' + number, label: 'Option ' + number };
                autoIds.add(option);
                options.push(option);
                markDirty();
                startEditing('option', options.length - 1, 'tpl-option-text');
            }, 'secondary', 'tpl-add-option'));
        }
        return section('Options', parts);
    }

    function optionForm(path, option, index) {
        return h('fieldset', { class: 'govuk-fieldset' }, [
            h('legend', { class: 'govuk-fieldset__legend govuk-fieldset__legend--m', text: 'Option ' + (index + 1) }),
            textInput({
                id: 'tpl-option-text', label: 'Text', value: option.label,
                onInput: function (value) {
                    option.label = value;
                    // Answers and rules already use the saved values of existing options, so only new ones follow the text.
                    if (autoIds.has(option)) {
                        option.value = lib.kebabCase(value) || value;
                        document.getElementById('tpl-option-value').value = option.value;
                    }
                    edited(path);
                }
            }),
            textInput({
                id: 'tpl-option-value', label: 'Saved value', value: option.value, code: true, width: 20,
                hint: autoIds.has(option) ? null : 'Saved answers and rules use this value. Only change it if nothing uses it yet.',
                onInput: function (value) { option.value = value; autoIds.delete(option); edited(path); }
            }),
            button('Done', function () { stopEditing('tpl-add-option'); }, 'secondary', 'tpl-option-done')
        ]);
    }

    function complexFieldEditor(path, field, type) {
        var config = field.complexField || (field.complexField = { id: '' });
        var ids = complexFieldIds(type);
        var known = !!complexFieldConfig(config.id);
        var items = ids.map(function (id) { return { value: id, label: id }; });
        if (config.id && !known) items.unshift({ value: config.id, label: config.id + ' (not set up for this tenant)' });
        if (!items.length) items.push({ value: '', label: 'Nothing is set up for this tenant' });

        return section(type === 'upload' ? 'Upload settings' : 'Search settings', [
            selectInput({
                id: 'tpl-complex-id',
                label: type === 'upload' ? 'Upload field' : 'What to search',
                hint: 'These come from FormEngine:ComplexFields in Tenant settings.',
                value: config.id,
                items: items,
                onChange: function (value) { config.id = value; restructured(); }
            }),
            config.id && !known
                ? warningText('No ' + (type === 'upload' ? 'upload' : 'search') + ' field called "' + config.id + '" is set up for this tenant. Applicants will see an error until it is added in Tenant settings.')
                : null,
            type === 'search' ? searchDisplaySettings(path, config) : null
        ]);
    }

    var SEARCH_DISPLAYS = [
        { key: 'dropdownDisplay', label: 'Each search result', hint: 'How each result in the list of matches looks.' },
        { key: 'confirmationDisplay', label: 'The "Is this the right one?" box', hint: 'What applicants check before they confirm their choice. This replaces the whole box.' },
        { key: 'summaryDisplay', label: 'The answer on summary pages', hint: 'How the chosen result looks when applicants check their answers.' }
    ];

    // Expressions such as  name + " (" + ukprn + ")"  that override the tenant's layout for this question only.
    function searchDisplaySettings(path, config) {
        var tenant = complexFieldConfig(config.id) || {};
        var changed = SEARCH_DISPLAYS.some(function (display) { return !!config[display.key]; });
        var details = h('details', { class: 'govuk-details' }, [
            h('summary', { class: 'govuk-details__summary' }, h('span', { class: 'govuk-details__summary-text', text: 'How results are shown (optional)' })),
            h('div', { class: 'govuk-details__text' }, [
                h('p', { class: 'govuk-body', text: 'Leave these blank to use the tenant\'s layout. Join the result\'s properties and text with +, for example  name + " (" + ukprn + ")". You can use Markdown, such as ** for bold.' }),
                SEARCH_DISPLAYS.map(function (display) {
                    var current = tenant[display.key];
                    return textArea({
                        id: 'tpl-complex-' + display.key, label: display.label, value: config[display.key], rows: 2, code: true,
                        hint: display.hint + (current ? ' The tenant uses: ' + current : ' The tenant uses the built-in layout.'),
                        onInput: function (value) {
                            if (value.trim()) config[display.key] = value; else delete config[display.key];
                            edited(path);
                        }
                    });
                })
            ])
        ]);
        if (changed) details.setAttribute('open', '');
        return details;
    }

    function lengthEditor(path, field) {
        var rule = (field.validations || []).find(function (item) { return item.type === 'maxLength' || item.type === 'maxWords'; });
        var unit = rule && rule.type === 'maxWords' ? 'words' : 'characters';
        var parts = [
            textInput({
                id: 'tpl-limit', label: 'Maximum length (optional)', value: rule ? rule.rule : '', width: 5, numeric: true, suffix: unit,
                hint: field.type === 'character-count' ? 'Applicants see a count of what they have left.' : null,
                onChange: function (value) {
                    var number = parseInt(value, 10);
                    if (!value.trim() || isNaN(number) || number <= 0) {
                        field.validations = rulesOf(field).filter(function (item) { return item !== rule; });
                    } else if (rule) {
                        rule.rule = number;
                    } else {
                        rulesOf(field).push({ type: 'maxLength', rule: number, message: 'Must be ' + number + ' characters or fewer' });
                    }
                    restructured();
                }
            })
        ];

        if (rule) {
            parts.push(radios({
                name: 'tpl-limit-unit', legend: 'Count', inline: true, value: rule.type,
                items: [{ value: 'maxLength', label: 'Characters' }, { value: 'maxWords', label: 'Words' }],
                onChange: function (value) {
                    var unitAfter = value === 'maxWords' ? 'words' : 'characters';
                    rule.type = value;
                    if (typeof rule.message === 'string') rule.message = rule.message.replace(unit, unitAfter);
                    restructured();
                }
            }));
            parts.push(textInput({
                id: 'tpl-limit-message', label: 'Error message if it\'s too long', value: rule.message,
                onInput: function (value) { rule.message = value; edited(path); }
            }));
            parts.push(conditionSetting(path, rule, 'tpl-limit'));
        }
        return section('Length limit', parts);
    }

    function presetFor(rule) {
        return PATTERN_PRESETS.find(function (preset) { return preset.rule && preset.rule === rule.rule; });
    }

    function patternEditor(path, field) {
        var patterns = (field.validations || []).filter(function (rule) { return rule.type === 'regex'; });
        var parts = [];

        if (patterns.length) {
            parts.push(summaryList(patterns.map(function (rule, index) {
                var name = 'format check ' + (index + 1);
                return {
                    key: (presetFor(rule) || PATTERN_PRESETS[0]).label,
                    value: [
                        h('code', { text: rule.rule || '(no pattern)' }), h('br'), rule.message || 'No error message',
                        rule.condition ? [h('br'), h('span', { class: 'govuk-hint', text: conditionText(rule.condition) })] : null
                    ],
                    actions: [
                        { text: 'Change', hidden: name, onClick: function () { startEditing('pattern', index, 'tpl-pattern-rule'); } },
                        {
                            text: 'Remove', hidden: name, onClick: function () {
                                field.validations = rulesOf(field).filter(function (item) { return item !== rule; });
                                editing = null;
                                restructured();
                            }
                        }
                    ]
                };
            })));
        }

        if (isEditing('pattern') && patterns[editing.index]) {
            var rule = patterns[editing.index];
            parts.push(h('fieldset', { class: 'govuk-fieldset' }, [
                h('legend', { class: 'govuk-fieldset__legend govuk-fieldset__legend--m', text: 'Format check ' + (editing.index + 1) }),
                textInput({
                    id: 'tpl-pattern-rule', label: 'Pattern (regular expression)', value: rule.rule, code: true,
                    onInput: function (value) { rule.rule = value; edited(path); }
                }),
                textInput({
                    id: 'tpl-pattern-message', label: 'Error message if it doesn\'t match', value: rule.message,
                    onInput: function (value) { rule.message = value; edited(path); }
                }),
                conditionSetting(path, rule, 'tpl-pattern'),
                button('Done', function () { stopEditing('tpl-add-pattern'); }, 'secondary', 'tpl-pattern-done')
            ]));
        } else {
            parts.push(selectInput({
                id: 'tpl-pattern-preset', label: 'Add a format check', value: 'custom', items: PATTERN_PRESETS, onChange: function () { }
            }));
            parts.push(button('Add check', function () {
                var choice = document.getElementById('tpl-pattern-preset').value;
                var preset = PATTERN_PRESETS.find(function (item) { return item.value === choice; }) || PATTERN_PRESETS[0];
                rulesOf(field).push({ type: 'regex', rule: preset.rule, message: preset.message });
                markDirty();
                startEditing('pattern', patterns.length, 'tpl-pattern-rule');
            }, 'secondary', 'tpl-add-pattern'));
        }

        return section('Format checks', parts);
    }

    function numberOrNull(value) {
        var number = parseInt(value, 10);
        return isNaN(number) ? null : number;
    }

    function flowFieldIds(flow) {
        var ids = [];
        (flow.pages || []).forEach(function (page) {
            (page.fields || []).forEach(function (field) { if (field.fieldId) ids.push(field.fieldId); });
        });
        return ids;
    }

    function flowInspector(path, flow) {
        var answerIds = flowFieldIds(flow);
        return [
            header(path),
            nameInput(path, {
                id: 'tpl-flow-title', label: 'List name', kind: 'flow', idKey: 'flowId',
                get: function (node) { return node.title; },
                set: function (node, value) { node.title = value; }
            }),
            textArea({
                id: 'tpl-flow-description', label: 'Description (optional)', value: flow.description, rows: 2,
                onInput: function (value) { flow.description = value || null; edited(path); }
            }),
            textInput({
                id: 'tpl-flow-kind', label: 'What is one item called?', value: flow.itemKind, width: 20,
                hint: 'For example "trust". Used in headings and messages.',
                onInput: function (value) { flow.itemKind = value || null; edited(path); }
            }),
            textInput({
                id: 'tpl-flow-kind-plural', label: 'What are several items called?', value: flow.itemKindPlural, width: 20,
                onInput: function (value) { flow.itemKindPlural = value || null; edited(path); }
            }),
            textInput({
                id: 'tpl-flow-add', label: 'Add button text', value: flow.addButtonLabel, width: 20,
                onInput: function (value) { flow.addButtonLabel = value; edited(path); }
            }),
            textInput({
                id: 'tpl-flow-min', label: 'Fewest items (optional)', value: flow.minItems, width: 3, numeric: true, suffix: 'items',
                onInput: function (value) { flow.minItems = numberOrNull(value); edited(path); }
            }),
            textInput({
                id: 'tpl-flow-max', label: 'Most items (optional)', value: flow.maxItems, width: 3, numeric: true, suffix: 'items',
                onInput: function (value) { flow.maxItems = numberOrNull(value); edited(path); }
            }),
            textInput({
                id: 'tpl-flow-title-binding', label: 'Answer used as each item\'s name', value: flow.itemTitleBinding, code: true,
                hint: answerIds.length
                    ? 'An answer id from this list\'s pages: ' + answerIds.join(', ') + '. For a search answer add the property, for example ' + answerIds[0] + '.name.'
                    : 'An answer id from this list\'s pages. Add pages and questions first.',
                onInput: function (value) { flow.itemTitleBinding = value || null; edited(path); }
            }),
            radios({
                name: 'tpl-flow-table', legend: 'Show added items as', inline: true, value: flow.tableType === 'list' ? 'list' : 'card',
                items: [{ value: 'card', label: 'Cards' }, { value: 'list', label: 'A list' }],
                onChange: function (value) { flow.tableType = value; edited(path); }
            }),
            idInput(path, 'flowId', 'List id', null),
            idInput(path, 'fieldId', 'Answer id for the whole list', inUse()
                ? 'Every item is saved under this id. If you change it, the old id is retired and the new one keeps its reporting key.'
                : 'Every item is saved under this id.'),
            summaryColumnsEditor(flow),
            section('Messages', [
                h('p', { class: 'govuk-body', text: 'Shown after an item is added, changed or removed. Include an answer like this: {trustSearch.name}.' }),
                textInput({
                    id: 'tpl-flow-added', label: 'When an item is added (optional)', value: flow.addItemMessage,
                    onInput: function (value) { flow.addItemMessage = value || null; edited(path); }
                }),
                textInput({
                    id: 'tpl-flow-updated', label: 'When an item is changed (optional)', value: flow.updateItemMessage,
                    onInput: function (value) { flow.updateItemMessage = value || null; edited(path); }
                }),
                textInput({
                    id: 'tpl-flow-deleted', label: 'When an item is removed (optional)', value: flow.deleteItemMessage,
                    onInput: function (value) { flow.deleteItemMessage = value || null; edited(path); }
                })
            ]),
            contents(path, 'Pages for each item', 'This list has no pages yet.'),
            addForm({
                id: 'tpl-new-page', title: 'Add a page to each item', label: 'Page heading', button: 'Add page', fallback: 'New page',
                add: function (name) { addAndSelect(path.concat(['pages']), doc.newPage(name)); }
            })
        ];
    }

    function summaryColumnsEditor(flow) {
        // Only added to the JSON once someone adds a column.
        var columns = Array.isArray(flow.summaryColumns) ? flow.summaryColumns : [];
        var choices = flowFieldIds(flow).map(function (id) { return { value: id, label: id }; });
        return section('Columns in the list of added items', [
            h('p', { class: 'govuk-body', text: 'Answers shown for each item once it has been added.' }),
            listEditor({
                items: columns,
                noun: 'column',
                editKey: 'summaryColumn',
                firstInputId: 'tpl-summary-column-label',
                addId: 'tpl-add-summary-column',
                addText: 'Add a column',
                emptyText: 'No columns yet. Only each item\'s name is shown.',
                key: function (column) { return column.label || 'No heading'; },
                value: function (column) { return column.field || 'Answer not chosen'; },
                create: function () {
                    flow.summaryColumns = columns;
                    return { label: '', field: '' };
                },
                form: function (column) {
                    return [
                        textInput({
                            id: 'tpl-summary-column-label', label: 'Column heading', value: column.label,
                            onInput: function (value) { column.label = value; markDirty(); }
                        }),
                        selectInput({
                            id: 'tpl-summary-column-field', label: 'Answer', value: column.field,
                            items: [{ value: '', label: 'Choose an answer' }].concat(choices),
                            onChange: function (value) { column.field = value; markDirty(); }
                        })
                    ];
                }
            })
        ]);
    }

    // Lists, and search, checkboxes or dropdown questions, that pages can be repeated for.
    function derivedSources() {
        var sources = [];
        doc.eachTask(function (task) {
            ((task.summary && task.summary.flows) || []).forEach(function (flow) {
                if (flow.fieldId) sources.push({ value: flow.fieldId, label: (flow.title || flow.fieldId) + ' (repeatable list)', sourceType: 'collection' });
            });
        });
        doc.eachPageSet(function (pages) {
            pages.forEach(function (page) {
                (page.fields || []).forEach(function (field) {
                    var type = editorType(field);
                    var sourceType = { search: 'autocomplete', checkboxes: 'checkboxes', select: 'select' }[type];
                    if (sourceType && field.fieldId) {
                        sources.push({ value: field.fieldId, label: ((field.label && field.label.value) || field.fieldId) + ' (' + fieldTypeInfo(type).short.toLowerCase() + ')', sourceType: sourceType });
                    }
                });
            });
        });
        return sources;
    }

    function derivedFlowInspector(path, flow) {
        var sources = derivedSources();
        var items = sources.map(function (source) { return { value: source.value, label: source.label }; });
        if (flow.sourceFieldId && !sources.some(function (source) { return source.value === flow.sourceFieldId; })) {
            items.unshift({ value: flow.sourceFieldId, label: flow.sourceFieldId + ' (not in this template)' });
        }
        return [
            header(path),
            nameInput(path, {
                id: 'tpl-flow-title', label: 'Name', kind: 'derivedFlow', idKey: 'flowId',
                get: function (node) { return node.title; },
                set: function (node, value) { node.title = value; }
            }),
            selectInput({
                id: 'tpl-derived-source', label: 'Repeat the pages for each item in', value: flow.sourceFieldId,
                hint: 'A repeatable list, or a search, checkboxes or dropdown question.',
                items: [{ value: '', label: 'Choose a list or question' }].concat(items),
                onChange: function (value) {
                    flow.sourceFieldId = value;
                    var source = sources.find(function (item) { return item.value === value; });
                    if (source) flow.sourceType = source.sourceType;
                    restructured();
                }
            }),
            textInput({
                id: 'tpl-derived-item-title', label: 'Property used as each item\'s name', value: flow.itemTitleBinding, width: 20,
                hint: 'For example "name" for a trust found by search.',
                onInput: function (value) { flow.itemTitleBinding = value || 'name'; edited(path); }
            }),
            textInput({
                id: 'tpl-derived-empty', label: 'Message when there are no items yet (optional)', value: flow.emptyStateMessage,
                onInput: function (value) { flow.emptyStateMessage = value || null; edited(path); }
            }),
            textInput({
                id: 'tpl-derived-no-items', label: 'Error when there are no items (optional)', value: flow.noItemsErrorMessage,
                onInput: function (value) { flow.noItemsErrorMessage = value || null; edited(path); }
            }),
            textInput({
                id: 'tpl-derived-unsigned', label: 'Error when an item isn\'t complete (optional)', value: flow.unsignedItemErrorMessage,
                onInput: function (value) { flow.unsignedItemErrorMessage = value || null; edited(path); }
            }),
            selectInput({
                id: 'tpl-derived-source-type', label: 'That answer is', value: flow.sourceType || 'autocomplete',
                items: [
                    { value: 'collection', label: 'A repeatable list' },
                    { value: 'autocomplete', label: 'A search question' },
                    { value: 'checkboxes', label: 'A checkboxes question' },
                    { value: 'select', label: 'A dropdown question' }
                ],
                onChange: function (value) { flow.sourceType = value; edited(path); }
            }),
            textInput({
                id: 'tpl-derived-signed', label: 'Message when an item is complete (optional)', value: flow.signedMessage,
                hint: 'Use {displayName} for the item\'s name.',
                onInput: function (value) { flow.signedMessage = value || null; edited(path); }
            }),
            idInput(path, 'flowId', 'Id', null),
            idInput(path, 'fieldId', 'Answer id for the whole list', inUse()
                ? 'Every item is saved under this id. If you change it, the old id is retired and the new one keeps its reporting key.'
                : 'Every item is saved under this id.'),
            contents(path, 'Pages for each item', 'This list has no pages yet.'),
            addForm({
                id: 'tpl-new-page', title: 'Add a page to each item', label: 'Page heading', button: 'Add page', fallback: 'New page',
                add: function (name) { addAndSelect(path.concat(['pages']), doc.newPage(name)); }
            })
        ];
    }

    // ---- Rules -------------------------------------------------------------------------------------------------

    function titleForId(id) {
        var path = doc.findById(id);
        return path ? '"' + nodeTitle(path) + '"' : '"' + id + '" (not in this template)';
    }

    function formatValue(value) {
        if (Array.isArray(value)) return value.map(formatValue).join(' or ');
        return '"' + String(value) + '"';
    }

    function describeConditions(group) {
        var joiner = (group && group.logicalOperator) === 'OR' ? ' or ' : ' and ';
        var text = ((group && group.conditions) || []).map(function (condition) {
            if (condition.conditions && condition.conditions.length) return '(' + describeConditions(condition) + ')';
            var operator = operatorText(condition);
            var value = NO_VALUE_OPERATORS.indexOf(condition.operator) !== -1 ? '' : ' ' + formatValue(condition.value);
            return titleForId(condition.triggerField) + ' ' + operator + value;
        }).join(joiner);
        return (group && group.logicalOperator) === 'NOT' ? 'not all of: ' + text : text;
    }

    function describeActions(rule) {
        return (rule.affectedElements || []).map(function (element) {
            return element.action + ' ' + element.elementType + ' ' + titleForId(element.elementId);
        }).join(', ');
    }

    // Questions on task pages. Rules work on the answers to a task's pages, not on answers inside repeatable lists.
    function taskQuestions() {
        var list = [];
        doc.eachTask(function (task) {
            (task.pages || []).forEach(function (page) {
                (page.fields || []).forEach(function (field) {
                    if (field.fieldId) list.push({ field: field, page: page, task: task });
                });
            });
        });
        return list;
    }

    function questionById(id) {
        var match = taskQuestions().find(function (question) { return question.field.fieldId === id; });
        return match ? match.field : null;
    }

    function questionText(field) {
        return ((field.label && field.label.value) || field.fieldId) + ' (' + field.fieldId + ')';
    }

    function questionSelect(options) {
        var groups = [];
        taskQuestions().forEach(function (question) {
            var name = question.task.taskName || question.task.taskId;
            var group = groups.find(function (item) { return item.name === name; });
            if (!group) groups.push(group = { name: name, questions: [] });
            group.questions.push(question.field);
        });
        return formGroup([
            h('label', { class: 'govuk-label govuk-label--s', for: options.id, text: options.label }),
            hint(options.id, options.hint),
            h('select', {
                class: 'govuk-select',
                id: options.id,
                value: options.value || '',
                'aria-describedby': describedBy(options.id, options.hint),
                onChange: function (e) { options.onChange(e.target.value); }
            }, [h('option', { value: '', text: 'Choose a question' })].concat(groups.map(function (group) {
                return h('optgroup', { label: group.name }, group.questions.map(function (field) {
                    return h('option', { value: field.fieldId, text: questionText(field) });
                }));
            })))
        ]);
    }

    function optionItems(field) {
        return ((field && field.options) || []).map(function (option) {
            return { value: option.value, label: option.label === option.value ? option.label : option.label + ' (' + option.value + ')' };
        });
    }

    // One answer to compare with: a choice from the question's options, or typed in.
    function answerValueInput(id, label, trigger, value, onChange) {
        var items = optionItems(trigger);
        if (items.length) {
            return selectInput({
                id: id, label: label, value: value == null ? '' : String(value),
                items: [{ value: '', label: 'Choose an answer' }].concat(items),
                onChange: onChange
            });
        }
        return textInput({ id: id, label: label, value: value, width: 20, onInput: onChange });
    }

    function checkboxGroup(options) {
        return formGroup(h('fieldset', { class: 'govuk-fieldset', 'aria-describedby': describedBy(options.name, options.hint) }, [
            h('legend', { class: 'govuk-fieldset__legend govuk-fieldset__legend--s', text: options.legend }),
            hint(options.name, options.hint),
            h('div', { class: 'govuk-checkboxes govuk-checkboxes--small', 'data-module': 'govuk-checkboxes' }, options.items.map(function (item, index) {
                var id = options.name + '-' + index;
                return h('div', { class: 'govuk-checkboxes__item' }, [
                    h('input', {
                        class: 'govuk-checkboxes__input', id: id, name: options.name, type: 'checkbox', value: item.value,
                        checked: options.values.indexOf(item.value) !== -1,
                        onChange: function (e) { options.onChange(item.value, e.target.checked); }
                    }),
                    h('label', { class: 'govuk-label govuk-checkboxes__label', for: id, text: item.label })
                ]);
            }))
        ]));
    }

    // Adds a GOV.UK error message to a form group built by the helpers above.
    function withError(group, message) {
        if (!message || !group) return group;
        group.classList.add('govuk-form-group--error');
        var holder = group.querySelector('fieldset') || group;
        var anchor = holder.querySelector(':scope > .govuk-hint') || holder.querySelector(':scope > legend, :scope > label');
        var error = h('p', { class: 'govuk-error-message' }, [h('span', { class: 'govuk-visually-hidden', text: 'Error: ' }), message]);
        if (anchor) anchor.parentNode.insertBefore(error, anchor.nextSibling); else holder.insertBefore(error, holder.firstChild);
        group.querySelectorAll('.govuk-input').forEach(function (input) { input.classList.add('govuk-input--error'); });
        group.querySelectorAll('.govuk-select').forEach(function (input) { input.classList.add('govuk-select--error'); });
        return group;
    }

    function toggle(list, value, on) {
        var index = list.indexOf(value);
        if (on && index === -1) list.push(value);
        if (!on && index !== -1) list.splice(index, 1);
    }

    function blankCondition(triggerField) {
        return { triggerField: triggerField || '', operator: 'equals', value: '', dataType: 'string' };
    }

    function blankRuleDraft() {
        return {
            base: null, kind: 'show', name: '', enabled: true, match: 'all', conditions: [blankCondition()],
            pages: [], fields: [], setField: '', setValue: '', clearFields: [], targetTaskId: null
        };
    }

    function startRule(partial) {
        ruleDraft = Object.assign(blankRuleDraft(), partial || {});
        ruleErrors = {};
        confirmingRuleDelete = null;
        if (isShown(panels.rules)) renderRules(); else showTab('rules');
        if (!isShown(panels.rules)) renderRules();
        window.setTimeout(function () { focusById('tpl-rule-heading'); }, 0);
    }

    function pagesTasks() {
        var tasks = [];
        doc.eachTask(function (task) { if (Array.isArray(task.pages) && task.pages.length) tasks.push(task); });
        return tasks;
    }

    // The comparisons offered for a question, worded for how its answer is stored. Values are "operator|dataType".
    function comparisonsFor(trigger) {
        var type = trigger ? editorType(trigger) : 'text';
        var given = [{ value: 'isNotEmpty|string', label: 'has been given' }, { value: 'isEmpty|string', label: 'has not been given' }];
        if (type === 'radios' || type === 'select') {
            return [
                { value: 'equals|string', label: 'is' }, { value: 'notEquals|string', label: 'is not' },
                { value: 'in|string', label: 'is one of' }, { value: 'notIn|string', label: 'is not one of' }
            ].concat(given);
        }
        if (type === 'checkboxes') {
            return [{ value: 'equals|string', label: 'includes' }, { value: 'notEquals|string', label: 'does not include' }].concat(given);
        }
        if (type === 'date') {
            return [{ value: 'lessThan|date', label: 'is before' }, { value: 'greaterThan|date', label: 'is after' }].concat(given);
        }
        if (type === 'search' || type === 'upload') return given;
        return [
            { value: 'equals|string', label: 'is' }, { value: 'notEquals|string', label: 'is not' },
            { value: 'greaterThan|number', label: 'is more than (a number)' }, { value: 'lessThan|number', label: 'is less than (a number)' }
        ].concat(given);
    }

    function comparisonKey(condition) {
        return condition.operator + '|' + (condition.dataType || 'string');
    }

    function comparisonLabel(condition) {
        var match = comparisonsFor(questionById(condition.triggerField)).find(function (item) { return item.value === comparisonKey(condition); });
        return match ? match.label : operatorText(condition);
    }

    function isListComparison(condition) {
        return condition.operator === 'in' || condition.operator === 'notIn';
    }

    function conditionValueControl(condition, index) {
        if (VALUELESS.indexOf(condition.operator) !== -1) return null;
        var trigger = questionById(condition.triggerField);
        var id = 'tpl-rule-value-' + index;
        var items = optionItems(trigger);
        if (isListComparison(condition)) {
            if (!Array.isArray(condition.value)) condition.value = condition.value ? [condition.value] : [];
            if (items.length) {
                return withError(checkboxGroup({
                    name: id, legend: 'Answers', items: items, values: condition.value,
                    onChange: function (value, checked) { toggle(condition.value, value, checked); }
                }), ruleErrors[id + '-0']);
            }
            return withError(textInput({
                id: id, label: 'Answers', hint: 'Separate answers with commas.', value: condition.value.join(', '),
                onInput: function (value) { condition.value = value.split(',').map(function (item) { return item.trim(); }).filter(Boolean); }
            }), ruleErrors[id]);
        }
        if (Array.isArray(condition.value)) condition.value = condition.value[0] || '';
        if (condition.dataType === 'date') {
            return withError(textInput({
                id: id, label: 'Date', hint: 'Year, month and day, for example 2024-09-30', value: condition.value, width: 10, code: true,
                onInput: function (value) { condition.value = value.trim(); }
            }), ruleErrors[id]);
        }
        if (condition.dataType === 'number') {
            return withError(textInput({
                id: id, label: 'Number', value: condition.value, width: 5, numeric: true,
                onInput: function (value) { condition.value = value.trim(); }
            }), ruleErrors[id]);
        }
        return withError(answerValueInput(id, 'Answer', trigger, condition.value, function (value) { condition.value = value; }),
            ruleErrors[id]);
    }

    function conditionEditor(draft, condition, index) {
        var trigger = questionById(condition.triggerField);
        var comparisons = comparisonsFor(trigger);
        if (!comparisons.some(function (item) { return item.value === comparisonKey(condition); })) {
            var first = comparisons[0].value.split('|');
            condition.operator = first[0];
            condition.dataType = first[1];
        }
        var many = draft.conditions.length > 1;
        return h('div', { class: many ? 'govuk-inset-text govuk-!-margin-top-0' : null }, [
            many ? h('h3', { class: 'govuk-heading-s', text: 'Condition ' + (index + 1) }) : null,
            withError(questionSelect({
                id: 'tpl-rule-trigger-' + index, label: 'Question', value: condition.triggerField,
                onChange: function (value) {
                    condition.triggerField = value;
                    condition.value = '';
                    if (index === 0) draft.targetTaskId = null;
                    redrawDraft();
                }
            }), ruleErrors['tpl-rule-trigger-' + index]),
            selectInput({
                id: 'tpl-rule-comparison-' + index, label: 'The answer', value: comparisonKey(condition), items: comparisons,
                onChange: function (value) {
                    var parts = value.split('|');
                    var wasList = isListComparison(condition);
                    condition.operator = parts[0];
                    condition.dataType = parts[1];
                    if (wasList !== isListComparison(condition) || parts[1] !== 'string') condition.value = isListComparison(condition) ? [] : '';
                    redrawDraft();
                }
            }),
            conditionValueControl(condition, index),
            many ? h('p', { class: 'govuk-body' }, actionLink({
                text: 'Remove this condition', hidden: 'condition ' + (index + 1),
                onClick: function () {
                    draft.conditions.splice(index, 1);
                    redrawDraft();
                    focusById('tpl-rule-trigger-' + Math.max(0, index - 1));
                }
            })) : null
        ]);
    }

    function questionName(fieldId) {
        var field = questionById(fieldId);
        return field ? (field.label && field.label.value) || field.fieldId : fieldId;
    }

    function autoRuleName(draft) {
        var when = draft.conditions.map(function (condition) {
            var value = VALUELESS.indexOf(condition.operator) !== -1 ? '' : ' ' + formatValue(condition.value);
            return questionName(condition.triggerField) + ' ' + comparisonLabel(condition) + value;
        }).join(draft.match === 'any' ? ' or ' : ' and ');
        if (draft.kind === 'set') return 'Fill in ' + questionName(draft.setField) + ' when ' + when;
        if (draft.kind === 'clear') {
            return 'Clear ' + questionName(draft.clearFields[0]) + (draft.clearFields.length > 1 ? ' and ' + (draft.clearFields.length - 1) + ' more' : '') + ' when ' + when;
        }
        var targets = draft.pages.concat(draft.fields);
        var first = doc.findById(targets[0]);
        var what = (first ? nodeTitle(first) : targets[0]) + (targets.length > 1 ? ' and ' + (targets.length - 1) + ' more' : '');
        return 'Show ' + what + ' when ' + when;
    }

    function ruleDraftErrors(draft) {
        var errors = {};
        draft.conditions.forEach(function (condition, index) {
            var id = 'tpl-rule-value-' + index;
            var hasOptions = optionItems(questionById(condition.triggerField)).length > 0;
            if (!condition.triggerField) errors['tpl-rule-trigger-' + index] = 'Choose the question for condition ' + (index + 1);
            else if (VALUELESS.indexOf(condition.operator) !== -1) return;
            else if (isListComparison(condition)) {
                if (!condition.value.length) errors[hasOptions ? id + '-0' : id] = 'Choose at least one answer';
            } else if (condition.dataType === 'date' && !/^\d{4}-\d{2}-\d{2}$/.test(String(condition.value || ''))) {
                errors[id] = 'Enter a date as year, month and day, for example 2024-09-30';
            } else if (condition.dataType === 'number' && !/^-?\d+(\.\d+)?$/.test(String(condition.value || ''))) {
                errors[id] = 'Enter a number, like 10 or 2.5';
            } else if (!String(condition.value || '').trim()) {
                errors[id] = hasOptions ? 'Choose an answer' : 'Enter an answer';
            }
        });
        if (draft.kind === 'show' && !draft.pages.length && !draft.fields.length) {
            errors[document.getElementById('tpl-rule-pages-0') ? 'tpl-rule-pages-0' : 'tpl-rule-fields-0'] = 'Choose at least one page or question to show';
        }
        if (draft.kind === 'set') {
            if (!draft.setField) errors['tpl-rule-set-field'] = 'Choose the question to fill in';
            else if (!String(draft.setValue || '').trim()) errors['tpl-rule-set-value'] = 'Enter the answer to fill in';
        }
        if (draft.kind === 'clear' && !draft.clearFields.length) errors['tpl-rule-clear-0'] = 'Choose at least one question to clear';
        return errors;
    }

    function saveRule() {
        var draft = ruleDraft;
        ruleErrors = ruleDraftErrors(draft);
        if (Object.keys(ruleErrors).length) {
            renderRules();
            return;
        }

        if (!draft.name.trim()) draft.name = autoRuleName(draft);
        var base = doc.saveBuilderRule(draft);
        ruleDraft = null;
        markDirty();
        renderRules();
        focusById('tpl-rule-card-' + base);
    }

    // Questions in the chosen task, for showing or clearing.
    function taskTargets(draft) {
        var tasks = pagesTasks();
        if (!draft.targetTaskId) {
            var owner = taskQuestions().find(function (question) { return question.field.fieldId === draft.conditions[0].triggerField; });
            draft.targetTaskId = owner ? owner.task.taskId : tasks[0] && tasks[0].taskId;
        }
        var task = tasks.find(function (item) { return item.taskId === draft.targetTaskId; }) || tasks[0];
        var pages = (task && task.pages) || [];
        var triggers = draft.conditions.map(function (condition) { return condition.triggerField; });
        var questions = [];
        pages.forEach(function (page) {
            (page.fields || []).forEach(function (field) { if (triggers.indexOf(field.fieldId) === -1) questions.push(field); });
        });
        return {
            tasks: tasks,
            task: task,
            pages: pages,
            questions: questions,
            select: tasks.length > 1 ? selectInput({
                id: 'tpl-rule-task', label: 'Which task are they in?', value: task && task.taskId,
                items: tasks.map(function (item) { return { value: item.taskId, label: item.taskName || item.taskId }; }),
                onChange: function (value) { draft.targetTaskId = value; redrawDraft(); }
            }) : null
        };
    }

    function showTargets(draft) {
        var targets = taskTargets(draft);
        var elsewhere = draft.pages.filter(function (id) { return !targets.pages.some(function (page) { return page.pageId === id; }); })
            .concat(draft.fields.filter(function (id) { return !targets.questions.some(function (field) { return field.fieldId === id; }); }));
        var comparesNumbersOrDates = draft.conditions.some(function (condition) { return condition.dataType !== 'string'; });
        return [
            comparesNumbersOrDates
                ? inset('Pages set to "Go to the next page a rule shows" don\'t follow rules that compare numbers or dates.')
                : null,
            targets.select,
            withError(checkboxGroup({
                name: 'tpl-rule-pages', legend: 'Pages to show', hint: 'The pages are skipped when the conditions aren\'t met.',
                items: targets.pages.map(function (page) { return { value: page.pageId, label: page.title || page.pageId }; }),
                values: draft.pages,
                onChange: function (value, checked) { toggle(draft.pages, value, checked); }
            }), ruleErrors['tpl-rule-pages-0']),
            targets.questions.length ? withError(checkboxGroup({
                name: 'tpl-rule-fields', legend: 'Questions to show', hint: 'The questions are hidden when the conditions aren\'t met.',
                items: targets.questions.map(function (field) { return { value: field.fieldId, label: questionText(field) }; }),
                values: draft.fields,
                onChange: function (value, checked) { toggle(draft.fields, value, checked); }
            }), ruleErrors['tpl-rule-fields-0']) : null,
            elsewhere.length
                ? h('p', { class: 'govuk-body', text: 'It also shows ' + elsewhere.map(titleForId).join(', ') + ' from other tasks.' })
                : null
        ];
    }

    function setTargets(draft) {
        var field = questionById(draft.setField);
        return [
            withError(questionSelect({
                id: 'tpl-rule-set-field', label: 'Question to fill in', value: draft.setField,
                hint: 'The answer is filled in when the applicant saves a page and the conditions are met.',
                onChange: function (value) { draft.setField = value; draft.setValue = ''; redrawDraft(); }
            }), ruleErrors['tpl-rule-set-field']),
            draft.setField
                ? withError(answerValueInput('tpl-rule-set-value', 'Answer to fill in', field, draft.setValue, function (value) { draft.setValue = value; }),
                    ruleErrors['tpl-rule-set-value'])
                : null
        ];
    }

    function clearTargets(draft) {
        var targets = taskTargets(draft);
        var others = draft.clearFields.filter(function (id) { return !targets.questions.some(function (field) { return field.fieldId === id; }); });
        return [
            targets.select,
            withError(checkboxGroup({
                name: 'tpl-rule-clear', legend: 'Questions to clear',
                hint: 'Use this to remove answers that no longer apply, for example to questions a rule now hides.',
                items: targets.questions.map(function (field) { return { value: field.fieldId, label: questionText(field) }; }),
                values: draft.clearFields,
                onChange: function (value, checked) { toggle(draft.clearFields, value, checked); }
            }), ruleErrors['tpl-rule-clear-0']),
            others.length ? h('p', { class: 'govuk-body', text: 'It also clears ' + others.map(titleForId).join(', ') + ' from other tasks.' }) : null
        ];
    }

    function ruleBuilder() {
        var draft = ruleDraft;
        var errorIds = Object.keys(ruleErrors);
        var kindLegends = { show: 'Show the pages and questions when', set: 'Fill in the answer when', clear: 'Clear the answers when' };

        return h('div', { class: 'govuk-!-margin-bottom-9' }, [
            h('h2', { class: 'govuk-heading-l', id: 'tpl-rule-heading', tabindex: '-1', text: draft.base ? 'Change rule' : 'Add a rule' }),
            errorIds.length ? h('div', { class: 'govuk-error-summary', 'data-module': 'govuk-error-summary' }, h('div', { role: 'alert' }, [
                h('h2', { class: 'govuk-error-summary__title', text: 'There is a problem' }),
                h('div', { class: 'govuk-error-summary__body' }, h('ul', { class: 'govuk-list govuk-error-summary__list' }, errorIds.map(function (id) {
                    return h('li', null, h('a', { href: '#' + id, text: ruleErrors[id] }));
                })))
            ])) : null,
            textInput({
                id: 'tpl-rule-name', label: 'Rule name (optional)', value: draft.name,
                hint: 'Leave blank to name it from what it does.',
                onInput: function (value) { draft.name = value; }
            }),
            radios({
                name: 'tpl-rule-kind', legend: 'What should the rule do?', value: draft.kind,
                items: [
                    { value: 'show', label: 'Show pages or questions', hint: 'They stay hidden until the conditions are met.' },
                    { value: 'set', label: 'Fill in an answer' },
                    { value: 'clear', label: 'Clear answers' }
                ],
                onChange: function (value) { draft.kind = value; redrawDraft(); }
            }),
            h('h3', { class: 'govuk-heading-m', text: kindLegends[draft.kind] || kindLegends.show }),
            draft.conditions.length > 1 ? radios({
                name: 'tpl-rule-match', legend: 'Which conditions must be met?', value: draft.match, inline: true,
                items: [{ value: 'all', label: 'All of them' }, { value: 'any', label: 'Any of them' }],
                onChange: function (value) { draft.match = value; }
            }) : null,
            draft.conditions.map(function (condition, index) { return conditionEditor(draft, condition, index); }),
            button('Add another condition', function () {
                draft.conditions.push(blankCondition());
                redrawDraft();
                focusById('tpl-rule-trigger-' + (draft.conditions.length - 1));
            }, 'secondary', 'tpl-rule-add-condition'),
            h('h3', { class: 'govuk-heading-m', text: draft.kind === 'set' ? 'Answer' : draft.kind === 'clear' ? 'Answers to clear' : 'What it shows' }),
            draft.kind === 'set' ? setTargets(draft) : draft.kind === 'clear' ? clearTargets(draft) : showTargets(draft),
            h('div', { class: 'govuk-button-group' }, [
                button('Save rule', saveRule, null, 'tpl-rule-save'),
                h('a', {
                    href: '#tpl-rules-heading', class: 'govuk-link',
                    onClick: function (e) {
                        e.preventDefault();
                        ruleDraft = null;
                        ruleErrors = {};
                        renderRules();
                        focusById('tpl-add-rule');
                    }
                }, 'Cancel')
            ])
        ]);
    }

    function ruleCard(options) {
        var isConfirming = confirmingRuleDelete === options.deleteKey;
        var actions = isConfirming ? [] : options.actions.concat([
            { text: options.isOn ? 'Turn off' : 'Turn on', onClick: function () {
                options.rules.forEach(function (rule) { rule.enabled = !options.isOn; });
                markDirty();
                renderRules();
            } },
            { text: 'Delete', onClick: function () {
                confirmingRuleDelete = options.deleteKey;
                renderRules();
                focusById('tpl-rule-delete-yes');
            } }
        ]);
        return h('div', { class: 'govuk-summary-card' }, [
            h('div', { class: 'govuk-summary-card__title-wrapper' }, [
                h('h3', { class: 'govuk-summary-card__title', id: options.id, tabindex: '-1', text: options.title }),
                actions.length ? h('ul', { class: 'govuk-summary-card__actions' }, actions.map(function (action) {
                    return h('li', { class: 'govuk-summary-card__action' }, actionLink({ text: action.text, hidden: options.title, onClick: action.onClick }));
                })) : null
            ]),
            h('div', { class: 'govuk-summary-card__content' }, [
                summaryList(options.rows),
                options.note ? h('p', { class: 'govuk-hint govuk-!-margin-bottom-0', text: options.note }) : null,
                isConfirming ? h('div', { class: 'govuk-!-margin-top-4' }, [
                    warningText(options.deleteText || 'Are you sure you want to delete this rule?'),
                    h('div', { class: 'govuk-button-group' }, [
                        button('Yes, delete this rule', function () {
                            doc.removeRules(options.rules);
                            confirmingRuleDelete = null;
                            markDirty();
                            renderRules();
                            focusById('tpl-rules-heading');
                        }, 'warning', 'tpl-rule-delete-yes'),
                        h('a', {
                            href: '#' + options.id, class: 'govuk-link',
                            onClick: function (e) {
                                e.preventDefault();
                                confirmingRuleDelete = null;
                                renderRules();
                                focusById(options.id);
                            }
                        }, 'Cancel')
                    ])
                ]) : null
            ])
        ]);
    }

    function renderRules() {
        var focusId = rulesPanel.contains(document.activeElement) ? document.activeElement.id : null;
        var rules = doc.root.conditionalLogic || [];
        var entries = doc.builderRules();
        var paired = new Set();
        entries.forEach(function (entry) { entry.rules.forEach(function (rule) { paired.add(rule); }); });

        var content = [
            h('h2', { class: 'govuk-heading-l', id: 'tpl-rules-heading', tabindex: '-1', text: 'Rules' }),
            h('p', { class: 'govuk-body', text: 'Rules use earlier answers to show pages and questions, or to fill in or clear answers. Anything a rule shows is hidden until its conditions are met.' })
        ];

        if (ruleDraft) {
            content.push(ruleBuilder());
        } else if (taskQuestions().length) {
            content.push(button('Add a rule', function () { startRule(); }, null, 'tpl-add-rule'));
        } else {
            content.push(inset('Add questions to a task\'s pages before adding rules.'));
        }

        content.push(h('h2', { class: 'govuk-heading-m', text: 'Rules in this template' }));
        if (!rules.length) content.push(h('p', { class: 'govuk-body', text: 'This template has no rules yet.' }));

        entries.forEach(function (entry) {
            var isOn = entry.show.enabled !== false;
            var targets = (entry.show.affectedElements || []).map(function (element) {
                return (element.elementType === 'page' ? 'page ' : 'question ') + titleForId(element.elementId);
            }).join(', ');
            var then = entry.kind === 'set'
                ? { key: 'Fills in', value: targets + ' with ' + formatValue(((entry.show.affectedElements[0] || {}).actionConfig || {}).value) }
                : entry.kind === 'clear' ? { key: 'Clears', value: targets } : { key: 'Shows', value: targets };
            content.push(ruleCard({
                id: 'tpl-rule-card-' + entry.base,
                title: entry.show.name || entry.base,
                isOn: isOn,
                rules: entry.rules,
                deleteKey: entry.base,
                deleteText: entry.kind === 'show'
                    ? 'Are you sure you want to delete this rule? Pages and questions it shows will be shown to everyone.'
                    : 'Are you sure you want to delete this rule? Answers it ' + (entry.kind === 'set' ? 'fills in' : 'clears') + ' will be left as applicants give them.',
                actions: [{ text: 'Change', onClick: function () { startRule(doc.builderRuleDraft(entry)); } }],
                rows: [
                    { key: 'Status', value: isOn ? tag('On', 'green') : tag('Off', 'grey') },
                    { key: 'When', value: describeConditions(entry.show.conditionGroup) },
                    then
                ]
            }));
        });

        rules.filter(function (rule) { return !paired.has(rule); }).forEach(function (rule, index) {
            var isOn = rule.enabled !== false;
            content.push(ruleCard({
                id: 'tpl-rule-card-other-' + index,
                title: rule.name || rule.id || 'Rule ' + (index + 1),
                isOn: isOn,
                rules: [rule],
                deleteKey: rule,
                actions: [],
                note: 'Written in JSON. To change it, use the JSON tab.',
                rows: [
                    { key: 'Status', value: isOn ? tag('On', 'green') : tag('Off', 'grey') },
                    { key: 'When', value: describeConditions(rule.conditionGroup) || 'Always' },
                    { key: 'Then', value: describeActions(rule) || 'Nothing' }
                ]
            }));
        });

        rulesPanel.replaceChildren(h('div', null, content));
        enhance(rulesPanel);
        var showingErrors = !!ruleDraft && Object.keys(ruleErrors).length > 0;
        if (showingErrors && !govuk) focusById('tpl-rule-heading');
        else if (focusId && !showingErrors) focusById(focusId);
    }

    // The draft changed shape: drop old error messages and redraw the builder.
    function redrawDraft() {
        ruleErrors = {};
        renderRules();
    }

    // ---- Tabs and JSON -----------------------------------------------------------------------------------------

    function errorLocation(text, message) {
        var match = /line (\d+) column (\d+)/i.exec(message);
        if (match) return { line: Number(match[1]), column: Number(match[2]) };
        match = /position (\d+)/i.exec(message);
        if (!match) return null;
        var position = Number(match[1]);
        var before = text.slice(0, position).split('\n');
        return { line: before.length, column: before[before.length - 1].length + 1, position: position };
    }

    function showJsonError(error) {
        var location = errorLocation(jsonArea.value, error.message || '');
        jsonError.replaceChildren(h('span', { class: 'govuk-visually-hidden', text: 'Error: ' }), location
            ? 'The JSON isn\'t valid at line ' + location.line + ', column ' + location.column + '. Check for a missing or extra comma, quote or bracket there or just before it.'
            : 'The JSON isn\'t valid: ' + (error.message || 'check for a missing or extra comma, quote or bracket.'));
        jsonError.hidden = false;
        jsonGroup.classList.add('govuk-form-group--error');
        jsonArea.classList.add('govuk-textarea--error');
        jsonArea.focus();
        if (location && location.position != null) jsonArea.setSelectionRange(location.position, location.position + 1);
    }

    function clearJsonError() {
        jsonError.hidden = true;
        jsonError.replaceChildren();
        jsonGroup.classList.remove('govuk-form-group--error');
        jsonArea.classList.remove('govuk-textarea--error');
    }

    // Applies edits made in the JSON tab. Returns false, and shows why, when the JSON can't be used.
    function applyJson() {
        if (!jsonDirty) return true;
        try {
            doc = lib.TemplateDocument.parse(jsonArea.value);
        } catch (error) {
            showJsonError(error);
            return false;
        }
        jsonDirty = false;
        clearJsonError();
        autoIds = new WeakSet();
        expanded = new WeakSet();
        (doc.root.taskGroups || []).forEach(function (group) { expanded.add(group); });
        if (!doc.exists(selected)) selected = [];
        editing = null;
        confirmingDelete = false;
        moving = false;
        ruleDraft = null;
        ruleErrors = {};
        confirmingRuleDelete = null;
        if (committedJson === null) committedJson = doc.toJson();
        commitHistory();
        dirty = initialJson === null || doc.toJson() !== initialJson;
        updateStatus();
        // Every tab shows the new JSON, including the ones that aren't open, so nothing shows the old template.
        renderTree();
        renderInspector();
        renderRules();
        renderMappingWarnings();
        return true;
    }

    // The JSON to save or preview, or null if the JSON tab has a mistake in it.
    function currentSchema() {
        if (jsonDirty && !applyJson()) return null;
        return doc.toJson();
    }

    function isShown(panel) {
        return !panel.classList.contains('govuk-tabs__panel--hidden');
    }

    function tabLink(name) {
        return tabsElement.querySelector('.govuk-tabs__tab[href="#tpl-panel-' + name + '"]');
    }

    function showTab(name) {
        if (isShown(panels[name])) return;
        var link = tabLink(name);
        if (link) link.click();
    }

    // Stops GOV.UK Tabs leaving the JSON tab while the JSON can't be read. This runs before the Tabs component's
    // own listeners because it is registered on an ancestor in the capture phase.
    function guardTabChange(event) {
        if (event.type === 'keydown' && ['ArrowLeft', 'ArrowRight', 'Left', 'Right'].indexOf(event.key) === -1) return;
        var target = event.target.closest && event.target.closest('.govuk-tabs__tab');
        if (!target || !isShown(panels.json)) return;
        if (event.type === 'click' && target === tabLink('json')) return;
        if (!applyJson()) {
            event.preventDefault();
            event.stopPropagation();
        }
    }

    tabsElement.addEventListener('click', guardTabChange, true);
    tabsElement.addEventListener('keydown', guardTabChange, true);

    var shownBefore = {};
    function panelShown(name) {
        if (name === 'json' && !jsonDirty) {
            jsonArea.value = doc.toJson();
            clearJsonError();
        } else if (name === 'rules') {
            renderRules();
        } else if (name === 'design') {
            renderTree();
            renderInspector();
        }
    }

    var panelObserver = new MutationObserver(function () {
        Object.keys(panels).forEach(function (name) {
            var shown = isShown(panels[name]);
            if (shown && !shownBefore[name] && doc) panelShown(name);
            shownBefore[name] = shown;
        });
    });
    Object.keys(panels).forEach(function (name) {
        shownBefore[name] = isShown(panels[name]);
        panelObserver.observe(panels[name], { attributes: true, attributeFilter: ['class'] });
    });

    // On narrow screens GOV.UK Tabs shows every panel at once, so refresh the JSON when someone starts editing it.
    jsonArea.addEventListener('focus', function () {
        if (jsonDirty || !doc) return;
        var json = doc.toJson();
        if (jsonArea.value !== json) jsonArea.value = json;
    });

    jsonArea.addEventListener('input', function () {
        jsonDirty = true;
        markDirty();
    });

    // On narrow screens every panel is shown and there's no tab to leave, so apply the JSON when the box loses focus.
    jsonArea.addEventListener('change', function () {
        if (jsonDirty) applyJson();
    });

    document.getElementById('tpl-json-format').addEventListener('click', function () {
        try {
            jsonArea.value = JSON.stringify(JSON.parse(jsonArea.value), null, 2);
            clearJsonError();
        } catch (error) {
            showJsonError(error);
        }
    });

    // ---- Problems, preview and save ----------------------------------------------------------------------------

    function targetFor(message) {
        var quoted = (message.match(/'[^']+'|"[^"]+"/g) || []).map(function (text) { return text.slice(1, -1); });
        // Messages name the most specific thing first, for example "Field 'x' on page 'y' in task 'z'".
        for (var i = 0; i < quoted.length; i++) {
            var path = doc.findById(quoted[i]) || doc.findByName(quoted[i]);
            if (path) return path;
        }
        return null;
    }

    function showProblems(errors, failedToRun) {
        if (!errors.length) {
            problems.replaceChildren(h('div', {
                class: 'govuk-notification-banner govuk-notification-banner--success',
                role: 'alert',
                'aria-labelledby': 'tpl-problems-title',
                'data-module': 'govuk-notification-banner'
            }, [
                h('div', { class: 'govuk-notification-banner__header' },
                    h('h2', { class: 'govuk-notification-banner__title', id: 'tpl-problems-title', text: 'Success' })),
                h('div', { class: 'govuk-notification-banner__content' }, [
                    h('p', { class: 'govuk-notification-banner__heading', text: 'No problems found. This template can be saved.' }),
                    doc.missingMappedFields(eventMappings).length
                        ? h('p', { class: 'govuk-body' }, ['Some event mappings will need updating after you save. ', h('a', { class: 'govuk-notification-banner__link', href: '#tpl-mapping-warnings', text: 'See which answers they read' }), '.'])
                        : null
                ])
            ]));
            enhance(problems);
            if (!govuk) problems.focus();
            return;
        }

        var list = errors.map(function (message) {
            var target = failedToRun ? null : targetFor(message);
            return h('li', null, h('a', {
                href: target ? '#tpl-inspector-title' : '#template-studio-tabs',
                onClick: function (e) {
                    if (!target) return;
                    e.preventDefault();
                    showTab('design');
                    if (!isShown(panels.design)) return;
                    select(target, true);
                }
            }, message));
        });

        problems.replaceChildren(h('div', { class: 'govuk-error-summary', 'data-module': 'govuk-error-summary' }, h('div', { role: 'alert' }, [
            h('h2', { class: 'govuk-error-summary__title', text: failedToRun ? 'The check didn\'t run' : 'There is a problem' }),
            h('div', { class: 'govuk-error-summary__body' }, h('ul', { class: 'govuk-list govuk-error-summary__list' }, list))
        ])));
        enhance(problems);
        if (!govuk) problems.focus();
    }

    document.getElementById('tpl-check-button').addEventListener('click', function (e) {
        var schema = currentSchema();
        if (schema == null) return;
        var trigger = e.currentTarget;
        trigger.disabled = true;
        fetch(data.validateUrl, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': tokenInput ? tokenInput.value : ''
            },
            body: JSON.stringify({ schema: encodeSchema(schema) })
        })
            .then(function (response) {
                if (!response.ok) throw new Error('Status ' + response.status);
                return response.json();
            })
            .then(function (result) { showProblems((result.errors || []).concat(reportingProblems()), false); })
            .catch(function () {
                showProblems(['The check couldn\'t be run. Try again. Saving runs the same check.'], true);
            })
            .finally(function () { trigger.disabled = false; });
    });

    document.getElementById('tpl-preview-button').addEventListener('click', function () {
        var schema = currentSchema();
        if (schema == null) return;
        var taskPath = selected.length ? doc.ancestorPath(selected, 'task') : null;
        var taskId = taskPath ? doc.get(taskPath).taskId : '';
        document.getElementById('tpl-preview-schema').value = encodeSchema(schema);
        document.getElementById('tpl-preview-task').value = taskId || '';
        previewForm.submit();
    });

    // What the API would reject because applications already have answers. Empty when nothing uses the template.
    function reportingProblems() {
        if (!inUse()) return [];
        return doc.reportingIssues(baselineRoot).map(function (issue) { return issue.message; });
    }

    saveForm.addEventListener('submit', function (e) {
        var schema = currentSchema();
        if (schema == null) {
            e.preventDefault();
            showTab('json');
            return;
        }
        var reporting = reportingProblems();
        if (reporting.length) {
            e.preventDefault();
            showProblems(reporting, false);
            problems.scrollIntoView();
            return;
        }
        schemaInput.value = encodeSchema(schema);
        submitting = true;
    });

    function renderUsage() {
        var usage = document.getElementById('tpl-usage');
        if (!usage) {
            usage = h('div', { id: 'tpl-usage' });
            statusText.parentNode.insertBefore(usage, statusText.nextSibling);
        }
        if (!baselineFields.size) {
            usage.replaceChildren();
            return;
        }
        var text = applicationCount === null
            ? 'We couldn\'t check whether applications use this template, so Template Studio treats it as in use.'
            : applicationCount === 0
                ? 'No applications use this template yet, so you can change or delete any question.'
                : (applicationCount === 1 ? '1 application uses' : applicationCount + ' applications use') + ' this template.';
        if (applicationCount !== 0) {
            text += ' Deleting a question retires it and renaming an answer id keeps its reporting key, so reports keep earlier answers.';
        }
        usage.replaceChildren(inset(text));
    }

    document.getElementById('tpl-undo').addEventListener('click', undo);
    document.getElementById('tpl-redo').addEventListener('click', redo);

    // Leaving a box ends that undo step, so the next change is undone separately.
    container.addEventListener('focusout', function () {
        if (commitTimer) commitHistory();
    });

    // Ctrl+Z and Ctrl+Y (Cmd on a Mac) outside text boxes, which keep their own undo.
    document.addEventListener('keydown', function (e) {
        if (!(e.ctrlKey || e.metaKey) || e.altKey) return;
        var key = e.key.toLowerCase();
        if (key !== 'z' && key !== 'y') return;
        var target = e.target;
        if (target && (target.isContentEditable || /^(input|textarea|select)$/i.test(target.tagName))) return;
        if (!container.contains(target) && target !== document.body) return;
        e.preventDefault();
        if (key === 'y' || e.shiftKey) redo(); else undo();
    });

    window.addEventListener('beforeunload', function (e) {
        if (window.__flexformsAllowUnload) return;
        if (dirty && !submitting) {
            e.preventDefault();
            e.returnValue = '';
        }
    });

    var grantForm = document.getElementById('grant-template-to-all-users-form');
    if (grantForm) {
        grantForm.addEventListener('submit', function (e) {
            if (dirty && !window.confirm('You have unsaved changes. Granting access does not save this version. Continue?')) {
                e.preventDefault();
                e.stopImmediatePropagation();
                return;
            }
            window.__flexformsAllowUnload = true;
        });
    }

    if (data.templateId && window.history.replaceState) {
        var studioUrl = new URL(window.location.href);
        studioUrl.searchParams.set('templateId', data.templateId);
        if (data.versionNumber) studioUrl.searchParams.set('version', data.versionNumber);
        window.history.replaceState(null, '', studioUrl);
    }

    // ---- Start -------------------------------------------------------------------------------------------------

    container.hidden = false;
    updateStatus();
    renderUsage();

    // If GOV.UK Frontend didn't load, show every panel rather than leaving two of them unreachable.
    // govukReady never rejects: a failed import resolves to null.
    void govukReady.then(function () {
        window.setTimeout(function () {
            if (tabsElement.querySelector('[role="tablist"]')) return;
            Object.keys(panels).forEach(function (name) { panels[name].classList.remove('govuk-tabs__panel--hidden'); });
        }, 1000);
    });

    try {
        doc = lib.TemplateDocument.parse(data.schemaJson);
    } catch (error) {
        doc = new lib.TemplateDocument({ taskGroups: [] });
        jsonArea.value = data.schemaJson || '';
        jsonDirty = true;
        // GOV.UK Tabs opens the tab named in the address when it starts.
        if (window.history.replaceState) window.history.replaceState(null, '', '#tpl-panel-json');
        showJsonError(error);
        return;
    }

    (doc.root.taskGroups || []).forEach(function (group) { expanded.add(group); });
    committedJson = doc.toJson();
    initialJson = data.hasUnsavedChanges ? null : committedJson;
    renderTree();
    renderInspector();
    renderRules();
    renderMappingWarnings();
})();
