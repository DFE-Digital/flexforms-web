// Visual template editor. Builds GOV.UK Design System components over a TemplateDocument and writes every change
// straight into the JSON that is saved. No inline handlers: the Content-Security-Policy blocks them.
(function () {
    'use strict';

    var lib = window.FlexFormsTemplateDocument;
    var container = document.getElementById('template-editor');
    var dataElement = document.getElementById('template-editor-data');
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
        derivedFlow: 'Derived list'
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
        lessThanOrEqual: 'is at most', between: 'is between', isEmpty: 'is not answered', isNotEmpty: 'is answered',
        isTrue: 'is true', isFalse: 'is false', hasLength: 'has length', matchesPattern: 'matches',
        isValidEmail: 'is a valid email', isValidPhone: 'is a valid phone number'
    };

    var NO_VALUE_OPERATORS = ['isEmpty', 'isNotEmpty', 'isTrue', 'isFalse', 'isValidEmail', 'isValidPhone'];

    var tree = document.getElementById('tpl-tree');
    var inspector = document.getElementById('tpl-inspector');
    var rulesPanel = document.getElementById('tpl-rules');
    var problems = document.getElementById('tpl-problems');
    var statusText = document.getElementById('tpl-status');
    var tabsElement = document.getElementById('template-editor-tabs');
    var jsonArea = document.getElementById('tpl-json');
    var jsonGroup = document.getElementById('tpl-json-group');
    var jsonError = document.getElementById('tpl-json-error');
    var saveForm = document.getElementById('tpl-save-form');
    var schemaInput = document.getElementById('NewSchema');
    var previewForm = document.getElementById('tpl-preview-form');
    var tokenInput = saveForm.querySelector('input[name="__RequestVerificationToken"]');
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

    // ---- GOV.UK Frontend ---------------------------------------------------------------------------------------

    // The page template imports GOV.UK Frontend as a module. Importing the same URL shares that instance, so
    // components the editor adds after the page has loaded can be initialised the same way.
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
                if (mode === 'derivedCollectionFlow') return 'Task with derived lists';
                return 'Task';
            case 'field': return fieldTypeInfo(editorType(node)).short + (isRequired(node) ? '' : ', optional');
            default: return KIND_LABELS[doc.kindOf(path)];
        }
    }

    function childrenOf(path) {
        var node = doc.get(path);
        var kind = doc.kindOf(path);
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
        if (dirty) return;
        dirty = true;
        updateStatus();
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
            h('h2', { class: 'govuk-heading-s', text: 'Structure' }),
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
            h('summary', { class: 'govuk-details__summary' },
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
        return h('li', null, [
            editLink(path, title, null),
            h('br'),
            h('span', { class: 'govuk-hint govuk-!-font-size-16' }, type),
            targets.has(idOf(path)) ? [' ', tag('Shown by a rule', 'yellow')] : null
        ]);
    }

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
            path.length && confirmingDelete ? deleteConfirmation(path) : null
        ];
    }

    function nodeActions(path) {
        var index = path[path.length - 1];
        var siblings = doc.get(path.slice(0, -1));
        return h('div', { class: 'govuk-button-group' }, [
            index > 0 ? button('Move up', function () { selected = doc.move(path, -1); restructured(); }, 'secondary', 'tpl-move-up') : null,
            index < siblings.length - 1 ? button('Move down', function () { selected = doc.move(path, 1); restructured(); }, 'secondary', 'tpl-move-down') : null,
            button('Duplicate', function () {
                var copy = doc.duplicate(path);
                markDirty();
                select(copy, true);
            }, 'secondary', 'tpl-duplicate'),
            button('Delete', function () {
                confirmingDelete = true;
                renderInspector();
                focusById('tpl-delete-yes');
            }, 'warning', 'tpl-delete')
        ]);
    }

    function deleteConfirmation(path) {
        var kindLabel = KIND_LABELS[doc.kindOf(path)].toLowerCase();
        var message = 'Are you sure you want to delete this ' + kindLabel + (childrenOf(path).length ? ' and everything in it?' : '?');
        var references = doc.rulesReferencing(idOf(path)).length;
        if (references) message += ' ' + references + (references === 1 ? ' rule uses it and will need updating.' : ' rules use it and will need updating.');
        message += ' If applications already use this template, saving will tell you if removed questions need to be retired.';

        return h('div', { id: 'tpl-delete-confirm' }, [
            warningText(message),
            h('div', { class: 'govuk-button-group' }, [
                button('Yes, delete this ' + kindLabel, function () {
                    var parent = doc.parentPath(path);
                    doc.remove(path);
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
            onChange: function (value) {
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
                } else if (doc.kindOf(path) === 'field' || doc.kindOf(path) === 'page') {
                    var updated = doc.renameReferences(before, trimmed);
                    if (updated) {
                        text = 'Updated ' + updated + (updated === 1 ? ' rule' : ' rules') + ' to use the new id.';
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

    function ruleList(id) {
        var rules = doc.rulesReferencing(id);
        if (!rules.length) return null;
        return section('Rules that use this', h('ul', { class: 'govuk-list govuk-list--bullet' }, rules.map(function (rule) {
            return h('li', { text: (rule.enabled === false ? '(Turned off) ' : '') + 'When ' + describeConditions(rule.conditionGroup) + ': ' + describeActions(rule) + '.' });
        })));
    }

    function templateInspector(path) {
        var root = doc.root;
        var dashboard = (root.dashboard && root.dashboard.columns) || [];
        var metadata = (root.applicationMetaData && root.applicationMetaData.items) || [];
        var retired = root.retiredFields || [];
        var rules = root.conditionalLogic || [];

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
            section('Set up in the JSON tab', summaryList([
                { key: 'Dashboard columns', value: dashboard.length ? String(dashboard.length) : 'Default columns' },
                { key: 'Task list details', value: metadata.length ? metadata.length + (metadata.length === 1 ? ' row' : ' rows') : 'Default rows' },
                { key: 'Retired questions', value: String(retired.length) },
                { key: 'Rules', value: String(rules.length) }
            ], true))
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
                    items: [
                        { value: 'pages', label: 'Questions on one or more pages' },
                        { value: 'collection', label: 'A repeatable list', hint: 'Applicants add as many items as they need, for example trusts or academies.' }
                    ],
                    onChange: function () { }
                }),
                add: function (name) {
                    var shape = (inspector.querySelector('input[name="tpl-new-task-shape"]:checked') || {}).value;
                    addAndSelect(path.concat(['tasks']), doc.newTask(name, shape));
                }
            })
        ];
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
            })
        ];

        if (mode === 'multiCollectionFlow') {
            items.push(contents(path, 'Repeatable lists', 'This task has no lists yet.'));
            items.push(addForm({
                id: 'tpl-new-flow', title: 'Add a repeatable list', label: 'List name', button: 'Add list', fallback: 'New list',
                add: function (name) { addAndSelect(path.concat(['summary', 'flows']), doc.newFlow(name)); }
            }));
        } else if (mode === 'derivedCollectionFlow') {
            items.push(contents(path, 'Derived lists', 'This task has no lists yet.'));
            items.push(inset('Derived lists repeat pages for each item in another list. Set up new ones in the JSON tab.'));
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
            ruleList(page.pageId)
        ];
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
            idInput(path, 'fieldId', 'Answer id',
                'Answers are saved against this id. Changing it once applications exist loses their answers unless you retire the old id.'),
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
            return rule.condition || ['required', 'regex', 'maxLength', 'maxWords'].indexOf(rule.type) === -1
                || (TEXT_TYPES.indexOf(field.type) === -1 && (rule.type === 'maxLength' || rule.type === 'maxWords'));
        });
        if (other.length) items.push(inset(other.length + (other.length === 1 ? ' more check on this question is' : ' more checks on this question are') + ' set in the JSON tab.'));

        items.push(ruleList(field.fieldId));
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
                : null
        ]);
    }

    function lengthEditor(path, field) {
        var rule = findRule(field, ['maxLength', 'maxWords']);
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
        }
        return section('Length limit', parts);
    }

    function presetFor(rule) {
        return PATTERN_PRESETS.find(function (preset) { return preset.rule && preset.rule === rule.rule; });
    }

    function patternEditor(path, field) {
        var patterns = (field.validations || []).filter(function (rule) { return rule.type === 'regex' && !rule.condition; });
        var parts = [];

        if (patterns.length) {
            parts.push(summaryList(patterns.map(function (rule, index) {
                var name = 'format check ' + (index + 1);
                return {
                    key: (presetFor(rule) || PATTERN_PRESETS[0]).label,
                    value: [h('code', { text: rule.rule || '(no pattern)' }), h('br'), rule.message || 'No error message'],
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
            idInput(path, 'fieldId', 'Answer id for the whole list',
                'Every item is saved under this id. Changing it once applications exist loses their answers.'),
            inset('Summary columns and the added, updated and removed messages are set in the JSON tab.'),
            contents(path, 'Pages for each item', 'This list has no pages yet.'),
            addForm({
                id: 'tpl-new-page', title: 'Add a page to each item', label: 'Page heading', button: 'Add page', fallback: 'New page',
                add: function (name) { addAndSelect(path.concat(['pages']), doc.newPage(name)); }
            })
        ];
    }

    function derivedFlowInspector(path, flow) {
        return [
            header(path),
            nameInput(path, {
                id: 'tpl-flow-title', label: 'List name', kind: 'derivedFlow', idKey: 'flowId',
                get: function (node) { return node.title; },
                set: function (node, value) { node.title = value; }
            }),
            textInput({
                id: 'tpl-derived-source', label: 'Repeat for each item in', value: flow.sourceFieldId, code: true, width: 20,
                hint: 'The answer id of a list, or of a search or checkboxes question.',
                onInput: function (value) { flow.sourceFieldId = value; edited(path); }
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
            idInput(path, 'flowId', 'List id', null),
            idInput(path, 'fieldId', 'Answer id for the whole list',
                'Every item is saved under this id. Changing it once applications exist loses their answers.'),
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
            var operator = OPERATOR_TEXT[condition.operator] || condition.operator;
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

    function renderRules() {
        var rules = doc.root.conditionalLogic || [];
        var content = [
            h('h2', { class: 'govuk-heading-l', text: 'Rules' }),
            h('p', { class: 'govuk-body', text: 'Rules show, hide or skip pages and questions depending on answers. Anything a rule shows stays hidden until its condition is met, so most rules come in pairs: one to show and one to hide.' }),
            inset('You can turn rules on and off here. To add or change a rule, use the JSON tab. A rule builder is planned next.')
        ];

        if (!rules.length) {
            content.push(h('p', { class: 'govuk-body', text: 'This template has no rules.' }));
        }

        rules.forEach(function (rule, index) {
            var title = rule.name || rule.id || 'Rule ' + (index + 1);
            var isOn = rule.enabled !== false;
            content.push(h('div', { class: 'govuk-summary-card' }, [
                h('div', { class: 'govuk-summary-card__title-wrapper' }, [
                    h('h3', { class: 'govuk-summary-card__title', text: title }),
                    h('ul', { class: 'govuk-summary-card__actions' }, h('li', { class: 'govuk-summary-card__action' }, actionLink({
                        text: isOn ? 'Turn off' : 'Turn on',
                        hidden: title,
                        onClick: function () {
                            rule.enabled = !isOn;
                            markDirty();
                            renderRules();
                        }
                    })))
                ]),
                h('div', { class: 'govuk-summary-card__content' }, summaryList([
                    { key: 'Status', value: isOn ? tag('On', 'green') : tag('Off', 'grey') },
                    { key: 'When', value: describeConditions(rule.conditionGroup) || 'Always' },
                    { key: 'Then', value: describeActions(rule) || 'Nothing' }
                ]))
            ]));
        });

        rulesPanel.replaceChildren(h('div', null, content));
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
        var quoted = (message.match(/'([^']+)'/g) || []).map(function (text) { return text.slice(1, -1); });
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
                h('div', { class: 'govuk-notification-banner__content' },
                    h('p', { class: 'govuk-notification-banner__heading', text: 'No problems found. This template can be saved.' }))
            ]));
            enhance(problems);
            if (!govuk) problems.focus();
            return;
        }

        var list = errors.map(function (message) {
            var target = failedToRun ? null : targetFor(message);
            return h('li', null, h('a', {
                href: target ? '#tpl-inspector-title' : '#template-editor-tabs',
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
            body: JSON.stringify({ schema: schema })
        })
            .then(function (response) {
                if (!response.ok) throw new Error('Status ' + response.status);
                return response.json();
            })
            .then(function (result) { showProblems(result.errors || [], false); })
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
        document.getElementById('tpl-preview-schema').value = schema;
        document.getElementById('tpl-preview-task').value = taskId || '';
        previewForm.submit();
    });

    saveForm.addEventListener('submit', function (e) {
        var schema = currentSchema();
        if (schema == null) {
            e.preventDefault();
            showTab('json');
            return;
        }
        schemaInput.value = schema;
        submitting = true;
    });

    window.addEventListener('beforeunload', function (e) {
        if (dirty && !submitting) {
            e.preventDefault();
            e.returnValue = '';
        }
    });

    // ---- Start -------------------------------------------------------------------------------------------------

    container.hidden = false;
    updateStatus();

    // If GOV.UK Frontend didn't load, show every panel rather than leaving two of them unreachable.
    govukReady.then(function () {
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
    renderTree();
    renderInspector();
    renderRules();
})();
