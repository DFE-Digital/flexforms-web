// Visual template editor. Builds GOV.UK-styled controls over a TemplateDocument and writes every change straight
// into the JSON that is saved. No inline handlers: the Content-Security-Policy blocks them.
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
    var jsonArea = document.getElementById('tpl-json');
    var jsonError = document.getElementById('tpl-json-error');
    var saveForm = document.getElementById('tpl-save-form');
    var schemaInput = document.getElementById('NewSchema');
    var previewForm = document.getElementById('tpl-preview-form');
    var tokenInput = saveForm.querySelector('input[name="__RequestVerificationToken"]');

    var doc = null;
    var selected = [];
    var activeTab = 'design';
    var dirty = !!data.hasUnsavedChanges;
    var jsonDirty = false;
    var submitting = false;
    // Nodes created in this session get their ids from their names until someone edits the id.
    var autoIds = new WeakSet();
    var expanded = new WeakSet();
    var treeLabels = new Map();

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
        return formGroup([
            h('label', { class: 'govuk-label ' + (options.labelClass || 'govuk-label--s'), for: options.id, text: options.label }),
            hint(options.id, options.hint),
            h('input', {
                class: 'govuk-input' + (options.width ? ' govuk-input--width-' + options.width : '') + (options.code ? ' tpl-editor__code' : ''),
                id: options.id,
                type: 'text',
                value: options.value == null ? '' : String(options.value),
                inputmode: options.numeric ? 'numeric' : null,
                spellcheck: options.code || options.numeric ? 'false' : null,
                autocomplete: 'off',
                list: options.list || null,
                'aria-describedby': describedBy(options.id, options.hint, options.describedBy),
                onFocus: options.onFocus ? function (e) { options.onFocus(e.target.value); } : null,
                onInput: options.onInput ? function (e) { options.onInput(e.target.value, e.target); } : null,
                onChange: options.onChange ? function (e) { options.onChange(e.target.value, e.target); } : null
            }),
            options.after || null
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
        return formGroup(h('div', { class: 'govuk-checkboxes govuk-checkboxes--small' },
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

    function radios(options) {
        return formGroup(h('fieldset', { class: 'govuk-fieldset', 'aria-describedby': describedBy(options.name, options.hint) }, [
            h('legend', { class: 'govuk-fieldset__legend govuk-fieldset__legend--s', text: options.legend }),
            hint(options.name, options.hint),
            h('div', { class: 'govuk-radios govuk-radios--small' + (options.inline ? ' govuk-radios--inline' : '') },
                options.items.map(function (item, index) {
                    var id = options.name + '-' + index;
                    return h('div', { class: 'govuk-radios__item' }, [
                        h('input', {
                            class: 'govuk-radios__input',
                            id: id,
                            name: options.name,
                            type: 'radio',
                            value: item.value,
                            checked: item.value === options.value,
                            'aria-describedby': item.hint ? id + '-hint' : null,
                            onChange: function () { options.onChange(item.value); }
                        }),
                        h('label', { class: 'govuk-label govuk-radios__label', for: id, text: item.label }),
                        item.hint ? h('div', { class: 'govuk-hint govuk-radios__hint', id: id + '-hint', text: item.hint }) : null
                    ]);
                }))
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
            class: 'govuk-button govuk-!-margin-bottom-0' + (variant ? ' govuk-button--' + variant : ''),
            onClick: onClick
        }, text);
    }

    function linkButton(text, onClick, label) {
        return h('button', { type: 'button', class: 'tpl-editor__link-button govuk-link', 'aria-label': label || null, onClick: onClick }, text);
    }

    function section(title, children) {
        return h('div', { class: 'tpl-editor__section' }, [h('h3', { class: 'govuk-heading-s', text: title }), children]);
    }

    function inset(text) {
        return h('div', { class: 'govuk-inset-text govuk-!-margin-top-2', text: text });
    }

    function datalist(id, values) {
        return h('datalist', { id: id }, values.map(function (value) { return h('option', { value: value }); }));
    }

    function lowerFirst(text) {
        return text ? text.charAt(0).toLowerCase() + text.slice(1) : text;
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

    function setRequired(field, state) {
        var rules = rulesOf(field);
        if (state === 'required') {
            field.required = true;
            if (!findRule(field, ['required'])) rules.unshift({ type: 'required', rule: true, message: defaultRequiredMessage(field) });
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
            if (!Array.isArray(field.options) || field.options.length === 0) field.options = lib.defaultOptions(field.type);
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
            case 'template': return 'Template settings';
            case 'group': return 'Section';
            case 'task':
                var mode = node.summary && node.summary.mode;
                if (mode === 'multiCollectionFlow') return 'Task with repeatable lists';
                if (mode === 'derivedCollectionFlow') return 'Task with derived lists';
                return 'Task';
            case 'page': return 'Page';
            case 'field': return fieldTypeInfo(editorType(node)).short + (isRequired(node) ? '' : ', optional');
            case 'flow': return 'Repeatable list';
            default: return 'Derived list';
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
        statusText.textContent = dirty
            ? 'You have unsaved changes. Save a new version to keep them.'
            : 'No unsaved changes';
        statusText.classList.toggle('tpl-editor__status--dirty', dirty);
    }

    function markDirty() {
        if (dirty) return;
        dirty = true;
        updateStatus();
    }

    // A value changed: update the tree label in place so typing stays fast.
    function edited(path) {
        markDirty();
        var labels = treeLabels.get(lib.pathKey(path));
        if (labels) {
            labels.title.textContent = nodeTitle(path);
            labels.type.textContent = nodeType(path);
        }
        var heading = document.getElementById('tpl-inspector-title');
        if (heading && lib.samePath(path, selected)) heading.textContent = nodeTitle(path);
    }

    // The shape changed: rebuild the tree and the inspector.
    function restructured() {
        markDirty();
        renderTree();
        renderInspector();
    }

    function select(path) {
        selected = path;
        for (var end = 2; end <= path.length; end++) {
            if (typeof path[end - 1] === 'number') expanded.add(doc.get(path.slice(0, end)));
        }
        renderTree();
        renderInspector();
        var node = tree.querySelector('.tpl-tree__node--selected');
        if (node) node.scrollIntoView({ block: 'nearest' });
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

    // ---- Tree --------------------------------------------------------------------------------------------------

    function renderTree() {
        var focusKey = tree.contains(document.activeElement) ? document.activeElement.getAttribute('data-focus-key') : null;
        var targets = ruleTargets();
        treeLabels.clear();
        tree.replaceChildren(h('ul', { class: 'tpl-tree' }, treeItem([], 0, targets)));
        if (focusKey) {
            var target = tree.querySelector('[data-focus-key="' + focusKey + '"]')
                || tree.querySelector('[data-focus-key="node:' + lib.pathKey(selected) + '"]');
            if (target) target.focus();
        }
    }

    function treeItem(path, depth, targets) {
        var node = doc.get(path);
        var kind = doc.kindOf(path);
        var key = lib.pathKey(path);
        var children = childrenOf(path);
        var open = depth === 0 || expanded.has(node);
        var isSelected = lib.samePath(path, selected);
        var title = h('span', { class: 'tpl-tree__title', text: nodeTitle(path) });
        var type = h('span', { class: 'tpl-tree__type', text: nodeType(path) });
        var ruleTagged = targets.has(idOf(path));
        treeLabels.set(key, { title: title, type: type });

        var toggle = children.length && depth > 0
            ? h('button', {
                type: 'button',
                class: 'tpl-tree__toggle',
                'data-focus-key': 'toggle:' + key,
                'aria-expanded': String(open),
                'aria-label': (open ? 'Collapse ' : 'Expand ') + nodeTitle(path),
                onClick: function () {
                    if (open) expanded.delete(node); else expanded.add(node);
                    renderTree();
                }
            }, open ? '\u25BE' : '\u25B8')
            : h('span', { class: 'tpl-tree__spacer', 'aria-hidden': 'true' });

        return h('li', { class: 'tpl-tree__item tpl-tree__item--' + kind }, [
            h('div', { class: 'tpl-tree__row' }, [
                toggle,
                h('button', {
                    type: 'button',
                    class: 'tpl-tree__node' + (isSelected ? ' tpl-tree__node--selected' : ''),
                    'data-focus-key': 'node:' + key,
                    'aria-current': isSelected ? 'true' : null,
                    onClick: function () { select(path); }
                }, [
                    title,
                    type,
                    ruleTagged ? h('span', { class: 'tpl-tree__rule', text: 'Shown by a rule' }) : null
                ])
            ]),
            open && children.length
                ? h('ul', { class: 'tpl-tree__children' }, children.map(function (child) { return treeItem(child, depth + 1, targets); }))
                : null
        ]);
    }

    // ---- Inspector ---------------------------------------------------------------------------------------------

    function renderInspector() {
        var focusId = inspector.contains(document.activeElement) ? document.activeElement.id : null;
        if (!doc.exists(selected)) selected = [];
        var path = selected;
        var builders = {
            template: templateInspector,
            group: groupInspector,
            task: taskInspector,
            page: pageInspector,
            field: fieldInspector,
            flow: flowInspector,
            derivedFlow: derivedFlowInspector
        };
        var kind = doc.kindOf(path);
        inspector.replaceChildren(h('div', { class: 'tpl-editor__inspector-body' }, builders[kind](path, doc.get(path))));
        if (focusId) {
            var target = document.getElementById(focusId);
            if (target) target.focus();
        }
    }

    function header(path, caption) {
        return [
            h('span', { class: 'govuk-caption-m', text: caption }),
            h('h2', { class: 'govuk-heading-m', id: 'tpl-inspector-title', tabindex: '-1', text: nodeTitle(path) }),
            path.length ? nodeActions(path) : null
        ];
    }

    function nodeActions(path) {
        var index = path[path.length - 1];
        var siblings = doc.get(path.slice(0, -1));
        return h('div', { class: 'govuk-button-group tpl-editor__node-actions' }, [
            index > 0 ? button('Move up', function () { selected = doc.move(path, -1); restructured(); }, 'secondary', 'tpl-move-up') : null,
            index < siblings.length - 1 ? button('Move down', function () { selected = doc.move(path, 1); restructured(); }, 'secondary', 'tpl-move-down') : null,
            button('Duplicate', function () {
                var copy = doc.duplicate(path);
                markDirty();
                select(copy);
            }, 'secondary', 'tpl-duplicate'),
            button('Delete', function () { confirmDelete(path); }, 'warning', 'tpl-delete')
        ]);
    }

    function confirmDelete(path) {
        var message = 'Delete "' + nodeTitle(path) + '"';
        var children = childrenOf(path).length;
        if (children) message += ' and everything in it';
        message += '?';
        var references = doc.rulesReferencing(idOf(path)).length;
        if (references) message += '\n\n' + references + ' rule(s) use it and will need updating.';
        message += '\n\nIf applications already use this template, removed questions must be retired. Saving will tell you if so.';
        if (!window.confirm(message)) return;
        var parent = doc.parentPath(path);
        doc.remove(path);
        selected = parent;
        restructured();
        var heading = document.getElementById('tpl-inspector-title');
        if (heading) heading.focus();
    }

    // Name input that also regenerates the id while the node is new.
    function nameInput(path, options) {
        var node = doc.get(path);
        return textInput({
            id: options.id,
            label: options.label,
            hint: options.hint,
            value: options.get(node),
            labelClass: 'govuk-label--s',
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
        var warning = h('p', { class: 'govuk-error-message', id: 'tpl-id-' + key + '-warning', hidden: true });
        return textInput({
            id: 'tpl-id-' + key,
            label: label,
            hint: hintText,
            value: node[key],
            code: true,
            describedBy: warning.id,
            after: warning,
            onFocus: function (value) { before = value; },
            onInput: function (value) {
                node[key] = value;
                autoIds.delete(node);
                edited(path);
            },
            onChange: function (value) {
                var trimmed = value.trim();
                var message = '';
                var isError = true;
                node[key] = '';
                var taken = trimmed && doc.collectIds()[bucketFor(path, key)].has(trimmed);
                node[key] = trimmed;
                if (!trimmed) {
                    message = 'Enter an id';
                } else if (taken) {
                    message = 'Something else in this template already uses this id';
                } else if (doc.kindOf(path) === 'field' || doc.kindOf(path) === 'page') {
                    var updated = doc.renameReferences(before, trimmed);
                    if (updated) {
                        message = 'Updated ' + updated + ' rule(s) to use the new id.';
                        isError = false;
                    }
                }
                warning.className = isError ? 'govuk-error-message' : 'govuk-hint';
                warning.textContent = message;
                warning.hidden = !message;
                before = trimmed;
                edited(path);
            }
        });
    }

    function addForm(options) {
        var id = options.id;
        var fields = [];
        if (options.choices) {
            fields.push(options.choices);
        }
        fields.push(textInput({ id: id, label: options.label, hint: options.hint, labelClass: 'govuk-label--s' }));
        fields.push(button(options.button, function () {
            var input = document.getElementById(id);
            var name = (input.value || '').trim() || options.fallback;
            options.add(name);
        }, 'secondary', id + '-button'));
        return section(options.title, fields);
    }

    function ruleList(id) {
        var rules = doc.rulesReferencing(id);
        if (!rules.length) return null;
        return section('Rules that use this', h('ul', { class: 'govuk-list govuk-list--bullet' }, rules.map(function (rule) {
            return h('li', null, describeRule(rule));
        })));
    }

    function templateInspector(path) {
        var root = doc.root;
        var dashboard = (root.dashboard && root.dashboard.columns) || [];
        var metadata = (root.applicationMetaData && root.applicationMetaData.items) || [];
        var retired = root.retiredFields || [];
        var rules = root.conditionalLogic || [];

        function summaryRow(key, value) {
            return h('div', { class: 'govuk-summary-list__row' }, [
                h('dt', { class: 'govuk-summary-list__key', text: key }),
                h('dd', { class: 'govuk-summary-list__value', text: value })
            ]);
        }

        return [
            header(path, 'Template settings'),
            textInput({
                id: 'tpl-template-name', label: 'Template name', value: root.templateName,
                onInput: function (value) { root.templateName = value; edited(path); }
            }),
            textArea({
                id: 'tpl-template-description', label: 'Description', value: root.description, rows: 2,
                onInput: function (value) { root.description = value; edited(path); }
            }),
            textInput({
                id: 'tpl-id-templateId', label: 'Template id', code: true, value: root.templateId,
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
                label: 'Use the page title as the question when a page has one question',
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
            section('Set up in the JSON tab', [
                h('dl', { class: 'govuk-summary-list govuk-summary-list--no-border' }, [
                    summaryRow('Dashboard columns', dashboard.length ? String(dashboard.length) : 'Default columns'),
                    summaryRow('Task list details', metadata.length ? metadata.length + ' row(s)' : 'Default rows'),
                    summaryRow('Retired questions', String(retired.length)),
                    summaryRow('Rules', String(rules.length))
                ])
            ]),
            addForm({
                id: 'tpl-new-group',
                title: 'Add a section',
                label: 'Section heading',
                hint: 'Sections group tasks on the task list.',
                button: 'Add section',
                fallback: 'New section',
                add: function (name) { addAndSelect(['taskGroups'], doc.newGroup(name)); }
            })
        ];
    }

    function groupInspector(path, group) {
        return [
            header(path, 'Section'),
            nameInput(path, {
                id: 'tpl-group-name', label: 'Section heading', kind: 'group', idKey: 'groupId',
                get: function (node) { return node.groupName; },
                set: function (node, value) { node.groupName = value; }
            }),
            idInput(path, 'groupId', 'Section id', null),
            (group.tasks || []).length ? null : inset('Add at least one task. A section without tasks can\'t be saved.'),
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
            header(path, 'Task'),
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
            items.push(addForm({
                id: 'tpl-new-flow', title: 'Add a repeatable list', label: 'List name', button: 'Add list', fallback: 'New list',
                add: function (name) { addAndSelect(path.concat(['summary', 'flows']), doc.newFlow(name)); }
            }));
        } else if (mode === 'derivedCollectionFlow') {
            items.push(inset('Derived lists repeat pages for each item in another list. Set them up in the JSON tab. You can edit their pages and questions here.'));
        } else {
            items.push(checkbox({
                id: 'tpl-task-start-first', label: 'Open the first page when the task hasn\'t been started',
                hint: 'Otherwise applicants see the task summary first.',
                checked: task.startAtFirstPageWhenNotStarted === true,
                onChange: function (checked) { task.startAtFirstPageWhenNotStarted = checked || null; edited(path); }
            }));
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
            header(path, 'Page'),
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
        var requirement = requiredState(field);
        var requiredRule = findRule(field, ['required']);
        var policy = doc.root.defaultFieldRequirementPolicy === 'required' ? 'required' : 'optional';

        var items = [
            header(path, 'Question'),
            selectInput({
                id: 'tpl-field-type', label: 'Question type', value: type, items: FIELD_TYPES,
                onChange: function (value) { changeFieldType(field, value); restructured(); }
            }),
            nameInput(path, {
                id: 'tpl-field-label', label: 'Question', kind: 'field', idKey: 'fieldId',
                get: function (node) { return node.label.value; },
                set: function (node, value) { node.label.value = value; }
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
                value: requirement,
                inline: false,
                items: [
                    { value: 'required', label: 'Yes' },
                    { value: 'optional', label: 'No' },
                    { value: 'default', label: 'Use the template setting (' + policy + ')' }
                ],
                onChange: function (value) { setRequired(field, value); restructured(); }
            })
        ];

        if (requiredRule) {
            items.push(textInput({
                id: 'tpl-field-required-message', label: 'Error message if it isn\'t answered', value: requiredRule.message,
                onInput: function (value) { requiredRule.message = value; edited(path); }
            }));
        }

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
        if (other.length) items.push(inset(other.length + ' more check(s) on this question are set in the JSON tab.'));

        items.push(ruleList(field.fieldId));
        return items;
    }

    function optionsEditor(path, field) {
        var options = field.options || (field.options = []);
        var rows = options.map(function (option, index) {
            var valueId = 'tpl-option-' + index + '-value';
            return h('tr', { class: 'govuk-table__row' }, [
                h('td', { class: 'govuk-table__cell' }, h('input', {
                    class: 'govuk-input', id: 'tpl-option-' + index + '-label', type: 'text', value: option.label,
                    'aria-label': 'Option ' + (index + 1) + ' text',
                    onInput: function (e) {
                        var previous = option.label;
                        option.label = e.target.value;
                        if (!option.value || option.value === previous || option.value === lib.kebabCase(previous)) {
                            option.value = option.value === previous && previous ? e.target.value : lib.kebabCase(e.target.value) || e.target.value;
                            document.getElementById(valueId).value = option.value;
                        }
                        edited(path);
                    }
                })),
                h('td', { class: 'govuk-table__cell' }, h('input', {
                    class: 'govuk-input tpl-editor__code', id: valueId, type: 'text', value: option.value, spellcheck: 'false',
                    'aria-label': 'Option ' + (index + 1) + ' saved value',
                    onInput: function (e) { option.value = e.target.value; edited(path); }
                })),
                h('td', { class: 'govuk-table__cell tpl-editor__row-actions' }, [
                    index > 0 ? linkButton('Up', function () { options.splice(index - 1, 0, options.splice(index, 1)[0]); restructured(); }, 'Move option ' + (index + 1) + ' up') : null,
                    index < options.length - 1 ? linkButton('Down', function () { options.splice(index + 1, 0, options.splice(index, 1)[0]); restructured(); }, 'Move option ' + (index + 1) + ' down') : null,
                    linkButton('Remove', function () { options.splice(index, 1); restructured(); }, 'Remove option ' + (index + 1))
                ])
            ]);
        });

        return section('Options', [
            h('p', { class: 'govuk-hint', text: 'The text is what applicants see. The saved value is stored with the answer and used by rules.' }),
            h('table', { class: 'govuk-table tpl-editor__options' }, [
                h('thead', { class: 'govuk-table__head' }, h('tr', { class: 'govuk-table__row' }, [
                    h('th', { class: 'govuk-table__header', scope: 'col', text: 'Text' }),
                    h('th', { class: 'govuk-table__header', scope: 'col', text: 'Saved value' }),
                    h('th', { class: 'govuk-table__header', scope: 'col' }, h('span', { class: 'govuk-visually-hidden', text: 'Actions' }))
                ])),
                h('tbody', { class: 'govuk-table__body' }, rows)
            ]),
            button('Add an option', function () {
                var number = options.length + 1;
                options.push({ value: 'option-' + number, label: 'Option ' + number });
                restructured();
                var input = document.getElementById('tpl-option-' + (options.length - 1) + '-label');
                if (input) { input.focus(); input.select(); }
            }, 'secondary', 'tpl-add-option')
        ]);
    }

    function complexFieldEditor(path, field, type) {
        var config = field.complexField || (field.complexField = { id: '' });
        var ids = complexFieldIds(type);
        var listId = 'tpl-complex-ids';
        var warning = h('p', { class: 'govuk-error-message', id: 'tpl-complex-warning' });

        function check(value) {
            var known = complexFieldConfig(value);
            warning.textContent = value && !known
                ? 'No ' + (type === 'upload' ? 'upload' : 'search') + ' field called "' + value + '" is set up for this tenant. Add it in Tenant settings under FormEngine:ComplexFields.'
                : '';
            warning.hidden = !warning.textContent;
        }
        check(config.id);

        return section(type === 'upload' ? 'Upload settings' : 'Search settings', [
            textInput({
                id: 'tpl-complex-id', label: type === 'upload' ? 'Upload field' : 'What to search', value: config.id, code: true, list: listId,
                hint: ids.length ? 'Set up for this tenant: ' + ids.join(', ') : 'Nothing is set up for this tenant yet.',
                describedBy: warning.id,
                after: [warning, datalist(listId, ids)],
                onInput: function (value) { config.id = value.trim(); check(config.id); edited(path); },
                onChange: function () { restructured(); }
            })
        ]);
    }

    function lengthEditor(path, field) {
        var rule = findRule(field, ['maxLength', 'maxWords']);
        var parts = [
            textInput({
                id: 'tpl-limit', label: 'Maximum length (optional)', value: rule ? rule.rule : '', width: 5, numeric: true,
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
                    var unitBefore = rule.type === 'maxWords' ? 'words' : 'characters';
                    var unitAfter = value === 'maxWords' ? 'words' : 'characters';
                    rule.type = value;
                    if (typeof rule.message === 'string') rule.message = rule.message.replace(unitBefore, unitAfter);
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

    function patternEditor(path, field) {
        var patterns = (field.validations || []).filter(function (rule) { return rule.type === 'regex' && !rule.condition; });
        var parts = patterns.map(function (rule, index) {
            return h('div', { class: 'tpl-editor__pattern' }, [
                textInput({
                    id: 'tpl-pattern-' + index, label: 'Pattern ' + (index + 1) + ' (regular expression)', value: rule.rule, code: true,
                    onInput: function (value) { rule.rule = value; edited(path); }
                }),
                textInput({
                    id: 'tpl-pattern-' + index + '-message', label: 'Error message if it doesn\'t match', value: rule.message,
                    onInput: function (value) { rule.message = value; edited(path); }
                }),
                linkButton('Remove pattern ' + (index + 1), function () {
                    field.validations = rulesOf(field).filter(function (item) { return item !== rule; });
                    restructured();
                })
            ]);
        });

        parts.push(selectInput({
            id: 'tpl-pattern-preset', label: 'Add a format check', value: 'custom', items: PATTERN_PRESETS, onChange: function () { }
        }));
        parts.push(button('Add check', function () {
            var choice = document.getElementById('tpl-pattern-preset').value;
            var preset = PATTERN_PRESETS.find(function (item) { return item.value === choice; }) || PATTERN_PRESETS[0];
            rulesOf(field).push({ type: 'regex', rule: preset.rule, message: preset.message });
            restructured();
            var input = document.getElementById('tpl-pattern-' + patterns.length);
            if (input) input.focus();
        }, 'secondary', 'tpl-add-pattern'));

        return section('Format checks', parts);
    }

    function flowFieldIds(flow) {
        var ids = [];
        (flow.pages || []).forEach(function (page) {
            (page.fields || []).forEach(function (field) { if (field.fieldId) ids.push(field.fieldId); });
        });
        return ids;
    }

    function numberOrNull(value) {
        var number = parseInt(value, 10);
        return isNaN(number) ? null : number;
    }

    function flowInspector(path, flow) {
        return [
            header(path, 'Repeatable list'),
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
            h('div', { class: 'tpl-editor__inline' }, [
                textInput({
                    id: 'tpl-flow-min', label: 'Fewest items (optional)', value: flow.minItems, width: 3, numeric: true,
                    onInput: function (value) { flow.minItems = numberOrNull(value); edited(path); }
                }),
                textInput({
                    id: 'tpl-flow-max', label: 'Most items (optional)', value: flow.maxItems, width: 3, numeric: true,
                    onInput: function (value) { flow.maxItems = numberOrNull(value); edited(path); }
                })
            ]),
            textInput({
                id: 'tpl-flow-title-binding', label: 'Answer used as each item\'s name', value: flow.itemTitleBinding, code: true,
                list: 'tpl-flow-field-ids',
                hint: 'An answer id from this list\'s pages. For a search answer add the property, for example trustSearch.name.',
                after: datalist('tpl-flow-field-ids', flowFieldIds(flow)),
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
            addForm({
                id: 'tpl-new-page', title: 'Add a page to each item', label: 'Page heading', button: 'Add page', fallback: 'New page',
                add: function (name) { addAndSelect(path.concat(['pages']), doc.newPage(name)); }
            })
        ];
    }

    function derivedFlowInspector(path, flow) {
        var sources = Array.from(doc.collectIds().field);
        return [
            header(path, 'Derived list'),
            nameInput(path, {
                id: 'tpl-flow-title', label: 'List name', kind: 'derivedFlow', idKey: 'flowId',
                get: function (node) { return node.title; },
                set: function (node, value) { node.title = value; }
            }),
            textInput({
                id: 'tpl-derived-source', label: 'Repeat for each item in', value: flow.sourceFieldId, code: true, list: 'tpl-derived-sources',
                hint: 'The answer id of a list, or of a search or checkboxes question.',
                after: datalist('tpl-derived-sources', sources),
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

    function describeRule(rule) {
        var actions = (rule.affectedElements || []).map(function (element) {
            return element.action + ' ' + element.elementType + ' ' + titleForId(element.elementId);
        }).join(', ');
        return (rule.enabled === false ? '(Turned off) ' : '') + 'When ' + describeConditions(rule.conditionGroup) + ': ' + actions + '.';
    }

    function renderRules() {
        var rules = doc.root.conditionalLogic || [];
        var intro = [
            h('h2', { class: 'govuk-heading-m', text: 'Rules' }),
            h('p', { class: 'govuk-body', text: 'Rules show, hide or skip pages and questions depending on answers. Anything a rule shows stays hidden until its condition is met, so most rules come in pairs: one to show and one to hide.' }),
            inset('You can turn rules on and off here. To add or change a rule, use the JSON tab. A rule builder is planned next.')
        ];
        if (!rules.length) {
            rulesPanel.replaceChildren(h('div', null, [intro, h('p', { class: 'govuk-body', text: 'This template has no rules.' })]));
            return;
        }
        var cards = rules.map(function (rule, index) {
            return h('div', { class: 'govuk-summary-card' }, [
                h('div', { class: 'govuk-summary-card__title-wrapper' }, [
                    h('h3', { class: 'govuk-summary-card__title', text: rule.name || rule.id || 'Rule ' + (index + 1) }),
                    h('strong', { class: 'govuk-tag ' + (rule.enabled === false ? 'govuk-tag--grey' : 'govuk-tag--green'), text: rule.enabled === false ? 'Off' : 'On' })
                ]),
                h('div', { class: 'govuk-summary-card__content' }, [
                    h('p', { class: 'govuk-body', text: describeRule(rule) }),
                    checkbox({
                        id: 'tpl-rule-' + index + '-enabled', label: 'Rule is on', checked: rule.enabled !== false,
                        onChange: function (checked) { rule.enabled = checked; markDirty(); renderRules(); }
                    })
                ])
            ]);
        });
        rulesPanel.replaceChildren(h('div', null, [intro, cards]));
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
        jsonError.textContent = location
            ? 'The JSON isn\'t valid at line ' + location.line + ', column ' + location.column + '. Check for a missing or extra comma, quote or bracket there or just before it.'
            : 'The JSON isn\'t valid: ' + (error.message || 'check for a missing or extra comma, quote or bracket.');
        jsonError.hidden = false;
        jsonArea.classList.add('govuk-textarea--error');
        jsonArea.focus();
        if (location && location.position != null) jsonArea.setSelectionRange(location.position, location.position + 1);
    }

    function clearJsonError() {
        jsonError.hidden = true;
        jsonError.textContent = '';
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
        return true;
    }

    function setTabUi(name) {
        container.querySelectorAll('[data-tpl-tab]').forEach(function (tab) {
            var isActive = tab.getAttribute('data-tpl-tab') === name;
            tab.parentElement.classList.toggle('govuk-tabs__list-item--selected', isActive);
            tab.setAttribute('aria-selected', String(isActive));
            tab.setAttribute('tabindex', isActive ? '0' : '-1');
            document.getElementById(tab.getAttribute('aria-controls')).classList.toggle('govuk-tabs__panel--hidden', !isActive);
        });
    }

    function showTab(name) {
        if (name === activeTab) return true;
        if (activeTab === 'json' && !applyJson()) return false;
        activeTab = name;
        setTabUi(name);
        if (name === 'json') {
            jsonArea.value = doc.toJson();
            jsonDirty = false;
            clearJsonError();
        } else if (name === 'rules') {
            renderRules();
        } else {
            renderTree();
            renderInspector();
        }
        return true;
    }

    // The JSON to save or preview, or null if the JSON tab has a mistake in it.
    function currentSchema() {
        if (activeTab === 'json' && !applyJson()) return null;
        return doc.toJson();
    }

    container.querySelectorAll('[data-tpl-tab]').forEach(function (tab) {
        tab.addEventListener('click', function (e) {
            e.preventDefault();
            showTab(tab.getAttribute('data-tpl-tab'));
        });
        tab.addEventListener('keydown', function (e) {
            if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return;
            var tabs = Array.from(container.querySelectorAll('[data-tpl-tab]'));
            var next = tabs[(tabs.indexOf(tab) + (e.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length];
            if (showTab(next.getAttribute('data-tpl-tab'))) next.focus();
        });
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

    function showProblems(errors, failedToRun) {
        if (!errors.length) {
            problems.replaceChildren(h('div', { class: 'govuk-inset-text' }, 'No problems found. This template can be saved.'));
            problems.focus();
            return;
        }
        var list = errors.map(function (message) {
            var ids = (message.match(/'([^']+)'/g) || []).map(function (quoted) { return quoted.slice(1, -1); });
            var target = null;
            // Messages name the most specific thing first, for example "Field 'x' on page 'y' in task 'z'".
            for (var i = 0; i < ids.length && !target; i++) target = doc.findById(ids[i]) || doc.findByName(ids[i]);
            if (!target || failedToRun) return h('li', { text: message });
            return h('li', null, h('a', {
                href: '#tpl-inspector',
                class: 'govuk-link',
                onClick: function (e) {
                    e.preventDefault();
                    if (!showTab('design')) return;
                    select(target);
                    var heading = document.getElementById('tpl-inspector-title');
                    if (heading) heading.focus();
                }
            }, message));
        });
        problems.replaceChildren(h('div', { class: 'govuk-error-summary', 'data-module': 'govuk-error-summary' }, h('div', { role: 'alert' }, [
            h('h2', { class: 'govuk-error-summary__title', text: failedToRun ? 'The check didn\'t run' : 'There ' + (errors.length === 1 ? 'is 1 problem' : 'are ' + errors.length + ' problems') }),
            h('div', { class: 'govuk-error-summary__body' }, h('ul', { class: 'govuk-list govuk-error-summary__list' }, list))
        ])));
        problems.focus();
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

    try {
        doc = lib.TemplateDocument.parse(data.schemaJson);
    } catch (error) {
        doc = new lib.TemplateDocument({});
        activeTab = 'json';
        setTabUi('json');
        jsonArea.value = data.schemaJson || '';
        jsonDirty = true;
        showJsonError(error);
        return;
    }

    (doc.root.taskGroups || []).forEach(function (group) { expanded.add(group); });
    renderTree();
    renderInspector();
})();
