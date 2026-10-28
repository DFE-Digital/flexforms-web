// In-memory form template for Template Studio. It edits the raw JSON so properties Template Studio doesn't know
// about (semanticKey, retiredFields, ...) are saved unchanged. Nodes are addressed by paths such as
// ['taskGroups', 0, 'tasks', 1, 'pages', 0, 'fields', 2].
(function (root) {
    'use strict';

    // The engine sorts by these and also walks array order, so they're kept equal to the position.
    var ORDER_KEYS = { taskGroups: 'groupOrder', tasks: 'taskOrder', pages: 'pageOrder', fields: 'order', derivedFlows: 'sectionOrder' };

    var KIND_BY_CONTAINER = {
        taskGroups: 'group',
        tasks: 'task',
        pages: 'page',
        fields: 'field',
        flows: 'flow',
        derivedFlows: 'derivedFlow'
    };

    var ID_KEY_BY_KIND = {
        group: 'groupId',
        task: 'taskId',
        page: 'pageId',
        field: 'fieldId',
        flow: 'flowId',
        derivedFlow: 'flowId'
    };

    var CHOICE_TYPES = ['radios', 'checkboxes', 'select'];

    function isChoiceType(type) {
        return CHOICE_TYPES.indexOf(type) !== -1;
    }

    function words(text) {
        return String(text || '')
            .normalize('NFKD')
            .replace(/[\u0300-\u036f]/g, '')
            .replace(/[^A-Za-z0-9]+/g, ' ')
            .trim()
            .split(/\s+/)
            .filter(Boolean)
            .slice(0, 6);
    }

    function kebabCase(text) {
        return words(text).join('-').toLowerCase();
    }

    function camelCase(text) {
        return words(text).map(function (word, index) {
            var lower = word.toLowerCase();
            return index === 0 ? lower : lower.charAt(0).toUpperCase() + lower.slice(1);
        }).join('');
    }

    function clone(value) {
        return JSON.parse(JSON.stringify(value));
    }

    function pathKey(path) {
        return path.join('/');
    }

    function parsePathKey(key) {
        if (!key) return [];
        return key.split('/').map(function (part) { return /^\d+$/.test(part) ? Number(part) : part; });
    }

    function samePath(a, b) {
        return !!a && !!b && pathKey(a) === pathKey(b);
    }

    function startsWith(path, prefix) {
        if (path.length < prefix.length) return false;
        for (var i = 0; i < prefix.length; i++) {
            if (path[i] !== prefix[i]) return false;
        }
        return true;
    }

    function TemplateDocument(json) {
        this.root = json;
    }

    TemplateDocument.parse = function (text) {
        var parsed = JSON.parse(text);
        if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
            throw new Error('The template must be a JSON object, starting with {');
        }
        return new TemplateDocument(parsed);
    };

    TemplateDocument.prototype.toJson = function () {
        return JSON.stringify(this.root, null, 2);
    };

    TemplateDocument.prototype.get = function (path) {
        var node = this.root;
        for (var i = 0; i < path.length; i++) {
            if (node == null) return undefined;
            node = node[path[i]];
        }
        return node;
    };

    TemplateDocument.prototype.kindOf = function (path) {
        if (path.length === 0) return 'template';
        return KIND_BY_CONTAINER[path[path.length - 2]] || null;
    };

    TemplateDocument.prototype.exists = function (path) {
        return path.length === 0 || this.get(path) !== undefined;
    };

    // The nearest ancestor (or the node itself) of the given kind.
    TemplateDocument.prototype.ancestorPath = function (path, kind) {
        for (var end = path.length; end >= 2; end--) {
            var candidate = path.slice(0, end);
            if (typeof candidate[end - 1] === 'number' && this.kindOf(candidate) === kind) return candidate;
        }
        return null;
    };

    // The node that owns this one, skipping the 'summary' object between a task and its lists.
    TemplateDocument.prototype.parentPath = function (path) {
        var parent = path.slice(0, -2);
        if (parent[parent.length - 1] === 'summary') parent = parent.slice(0, -1);
        return parent;
    };

    // Sets obj[key]; undefined removes the property so the JSON stays tidy.
    TemplateDocument.prototype.set = function (path, key, value) {
        var node = this.get(path);
        if (value === undefined) {
            delete node[key];
        } else {
            node[key] = value;
        }
    };

    TemplateDocument.prototype.renumber = function (arrayPath) {
        var orderKey = ORDER_KEYS[arrayPath[arrayPath.length - 1]];
        var items = this.get(arrayPath);
        if (!orderKey || !Array.isArray(items)) return;
        items.forEach(function (item, index) {
            if (item && typeof item === 'object') item[orderKey] = index + 1;
        });
    };

    TemplateDocument.prototype.insert = function (arrayPath, node, index) {
        var parent = this.get(arrayPath.slice(0, -1));
        var key = arrayPath[arrayPath.length - 1];
        if (!Array.isArray(parent[key])) parent[key] = [];
        var items = parent[key];
        var at = index == null ? items.length : Math.max(0, Math.min(index, items.length));
        items.splice(at, 0, node);
        this.renumber(arrayPath);
        return arrayPath.concat([at]);
    };

    TemplateDocument.prototype.remove = function (path) {
        var arrayPath = path.slice(0, -1);
        var items = this.get(arrayPath);
        items.splice(path[path.length - 1], 1);
        this.renumber(arrayPath);
    };

    // Moves a node up (-1) or down (+1) within its list. Returns its new path.
    TemplateDocument.prototype.move = function (path, delta) {
        var arrayPath = path.slice(0, -1);
        var items = this.get(arrayPath);
        var from = path[path.length - 1];
        var to = from + delta;
        if (to < 0 || to >= items.length) return path;
        var node = items.splice(from, 1)[0];
        items.splice(to, 0, node);
        this.renumber(arrayPath);
        return arrayPath.concat([to]);
    };

    // Moves a node into another list of the same kind, for example a question to another page. Leave `index`
    // out to add it at the end. Returns its new path.
    TemplateDocument.prototype.moveTo = function (path, toArrayPath, index) {
        var fromArrayPath = path.slice(0, -1);
        var from = path[path.length - 1];
        var source = this.get(fromArrayPath);
        var owner = this.get(toArrayPath.slice(0, -1));
        var key = toArrayPath[toArrayPath.length - 1];
        if (!Array.isArray(owner[key])) owner[key] = [];
        var target = owner[key];

        var node = source.splice(from, 1)[0];
        var at = index == null ? target.length : index;
        if (source === target && from < at) at--;
        at = Math.max(0, Math.min(at, target.length));
        target.splice(at, 0, node);

        // Nothing of one kind lives inside a list of the same kind, so removing the node can't shift the target path.
        this.renumber(fromArrayPath);
        this.renumber(toArrayPath);
        return toArrayPath.concat([at]);
    };

    // Copies a node, giving it and everything inside it new ids. Returns the copy's path.
    TemplateDocument.prototype.duplicate = function (path) {
        var self = this;
        var copy = clone(this.get(path));
        var kind = this.kindOf(path);
        var nameKey = { group: 'groupName', task: 'taskName', page: 'title', flow: 'title' }[kind];
        if (nameKey && copy[nameKey]) copy[nameKey] = copy[nameKey] + ' (copy)';
        if (kind === 'field' && copy.label && copy.label.value) copy.label.value = copy.label.value + ' (copy)';

        var used = this.collectIds();
        (function reassign(node, nodeKind) {
            var idKey = ID_KEY_BY_KIND[nodeKind];
            if (idKey && node[idKey]) {
                var bucket = nodeKind === 'derivedFlow' ? 'flow' : nodeKind;
                node[idKey] = self.uniqueIdFrom(node[idKey] + (nodeKind === 'field' ? 'Copy' : '-copy'), used[bucket]);
                used[bucket].add(node[idKey]);
                if (nodeKind === 'page' && node.slug) node.slug = kebabCase(node[idKey]);
            }
            Object.keys(KIND_BY_CONTAINER).forEach(function (container) {
                var children = container === 'flows' || container === 'derivedFlows'
                    ? node.summary && node.summary[container]
                    : node[container];
                if (Array.isArray(children)) {
                    children.forEach(function (child) { reassign(child, KIND_BY_CONTAINER[container]); });
                }
            });
            if ((nodeKind === 'flow' || nodeKind === 'derivedFlow') && node.fieldId) {
                node.fieldId = self.uniqueIdFrom(node.fieldId + 'Copy', used.field);
                used.field.add(node.fieldId);
            }
        })(copy, kind);

        var arrayPath = path.slice(0, -1);
        return this.insert(arrayPath, copy, path[path.length - 1] + 1);
    };

    // Calls back with (pages, pagesPath, ownerPath) for every list of pages: task pages and collection pages.
    TemplateDocument.prototype.eachPageSet = function (callback) {
        var groups = this.root.taskGroups || [];
        groups.forEach(function (group, g) {
            (group.tasks || []).forEach(function (task, t) {
                var taskPath = ['taskGroups', g, 'tasks', t];
                if (Array.isArray(task.pages)) callback(task.pages, taskPath.concat(['pages']), taskPath);
                var summary = task.summary || {};
                ['flows', 'derivedFlows'].forEach(function (key) {
                    (summary[key] || []).forEach(function (flow, f) {
                        var flowPath = taskPath.concat(['summary', key, f]);
                        if (Array.isArray(flow.pages)) callback(flow.pages, flowPath.concat(['pages']), flowPath);
                    });
                });
            });
        });
    };

    TemplateDocument.prototype.eachTask = function (callback) {
        (this.root.taskGroups || []).forEach(function (group, g) {
            (group.tasks || []).forEach(function (task, t) {
                callback(task, ['taskGroups', g, 'tasks', t]);
            });
        });
    };

    TemplateDocument.prototype.collectIds = function () {
        var ids = { group: new Set(), task: new Set(), page: new Set(), field: new Set(), flow: new Set() };
        (this.root.taskGroups || []).forEach(function (group) { if (group.groupId) ids.group.add(group.groupId); });
        this.eachTask(function (task) {
            if (task.taskId) ids.task.add(task.taskId);
            var summary = task.summary || {};
            (summary.flows || []).concat(summary.derivedFlows || []).forEach(function (flow) {
                if (flow.flowId) ids.flow.add(flow.flowId);
                if (flow.fieldId) ids.field.add(flow.fieldId);
            });
        });
        this.eachPageSet(function (pages) {
            pages.forEach(function (page) {
                if (page.pageId) ids.page.add(page.pageId);
                (page.fields || []).forEach(function (field) { if (field.fieldId) ids.field.add(field.fieldId); });
            });
        });
        return ids;
    };

    // Event mapping references ({ fieldId, eventType, mappingId }) whose answer id isn't in the template any more.
    TemplateDocument.prototype.missingMappedFields = function (references) {
        var ids = new Set(Array.from(this.collectIds().field).map(function (id) { return id.toLowerCase(); }));
        return (references || []).filter(function (reference) {
            return reference && reference.fieldId && !ids.has(String(reference.fieldId).toLowerCase());
        });
    };

    TemplateDocument.prototype.uniqueIdFrom = function (base, used) {
        var id = base || 'item';
        if (!used.has(id)) return id;
        for (var n = 2; ; n++) {
            if (!used.has(id + '-' + n)) return id + '-' + n;
        }
    };

    // An id for a node of this kind made from its name, not used anywhere else in the template.
    TemplateDocument.prototype.idFor = function (kind, name, ignore) {
        var bucket = kind === 'derivedFlow' ? 'flow' : kind;
        var used = this.collectIds()[bucket];
        if (ignore) used.delete(ignore);
        var base = kind === 'field' || kind === 'flow' ? camelCase(name) : kebabCase(name);
        if (kind === 'page' && base) base += '-page';
        return this.uniqueIdFrom(base || kind, used);
    };

    // Finds the node with this id. Fields win over pages so rules and errors point at the question.
    TemplateDocument.prototype.findById = function (id) {
        var found = null;
        var self = this;
        this.eachPageSet(function (pages, pagesPath) {
            pages.forEach(function (page, p) {
                (page.fields || []).forEach(function (field, f) {
                    if (!found && field.fieldId === id) found = pagesPath.concat([p, 'fields', f]);
                });
            });
        });
        if (found) return found;
        this.eachPageSet(function (pages, pagesPath) {
            pages.forEach(function (page, p) {
                if (!found && page.pageId === id) found = pagesPath.concat([p]);
            });
        });
        if (found) return found;
        this.eachTask(function (task, taskPath) {
            if (!found && task.taskId === id) found = taskPath;
            var summary = task.summary || {};
            ['flows', 'derivedFlows'].forEach(function (key) {
                (summary[key] || []).forEach(function (flow, f) {
                    if (!found && (flow.flowId === id || flow.fieldId === id)) found = taskPath.concat(['summary', key, f]);
                });
            });
        });
        if (found) return found;
        (self.root.taskGroups || []).forEach(function (group, g) {
            if (!found && group.groupId === id) found = ['taskGroups', g];
        });
        return found;
    };

    // Finds a section, task, page or question by the name designers see. Validation messages use both.
    TemplateDocument.prototype.findByName = function (name) {
        var found = null;
        (this.root.taskGroups || []).forEach(function (group, g) {
            if (!found && group.groupName === name) found = ['taskGroups', g];
        });
        this.eachTask(function (task, taskPath) {
            if (!found && task.taskName === name) found = taskPath;
        });
        this.eachPageSet(function (pages, pagesPath) {
            pages.forEach(function (page, p) {
                if (!found && page.title === name) found = pagesPath.concat([p]);
                (page.fields || []).forEach(function (field, f) {
                    if (!found && field.label && field.label.value === name) found = pagesPath.concat([p, 'fields', f]);
                });
            });
        });
        return found;
    };

    function eachCondition(conditions, callback) {
        (conditions || []).forEach(function (condition) {
            callback(condition);
            eachCondition(condition.conditions, callback);
        });
    }

    // Rules whose conditions read this id or whose actions change it.
    TemplateDocument.prototype.rulesReferencing = function (id) {
        if (!id) return [];
        return (this.root.conditionalLogic || []).filter(function (rule) {
            var hit = (rule.affectedElements || []).some(function (element) { return element.elementId === id; });
            eachCondition(rule.conditionGroup && rule.conditionGroup.conditions, function (condition) {
                if (condition.triggerField === id) hit = true;
            });
            return hit;
        });
    };

    // Keeps conditional logic pointing at a field or page after its id changes. Returns how many rules changed.
    TemplateDocument.prototype.renameReferences = function (oldId, newId) {
        if (!oldId || !newId || oldId === newId) return 0;
        var changed = 0;
        (this.root.conditionalLogic || []).forEach(function (rule) {
            var touched = false;
            (rule.affectedElements || []).forEach(function (element) {
                if (element.elementId === oldId) { element.elementId = newId; touched = true; }
            });
            eachCondition(rule.conditionGroup && rule.conditionGroup.conditions, function (condition) {
                if (condition.triggerField === oldId) { condition.triggerField = newId; touched = true; }
            });
            if (touched) changed++;
        });
        return changed;
    };

    // Factories for new nodes. Every member the C# model marks as required is present, otherwise saving fails.
    TemplateDocument.prototype.newGroup = function (name) {
        return {
            groupId: this.idFor('group', name),
            groupName: name,
            groupOrder: 0,
            groupStatus: 'NotStarted',
            tasks: []
        };
    };

    TemplateDocument.prototype.newTask = function (name, shape) {
        var task = {
            taskId: this.idFor('task', name),
            taskName: name,
            taskOrder: 0,
            taskStatus: 'NotStarted',
            visibleInTaskList: true
        };
        this.setTaskShape(task, shape || 'pages');
        return task;
    };

    TemplateDocument.prototype.taskShape = function (task) {
        var mode = task.summary && task.summary.mode;
        if (mode === 'multiCollectionFlow') return 'collection';
        if (mode === 'derivedCollectionFlow') return 'derived';
        return 'pages';
    };

    // A task can only change shape while nothing has been added to it, so no pages or questions are lost.
    TemplateDocument.prototype.isTaskEmpty = function (task) {
        if (Array.isArray(task.pages) && task.pages.length) return false;
        var summary = task.summary || {};
        return (summary.flows || []).concat(summary.derivedFlows || []).every(function (flow) {
            return !(flow.pages || []).length;
        });
    };

    TemplateDocument.prototype.setTaskShape = function (task, shape) {
        if (shape === 'collection') {
            task.pages = null;
            task.summary = { mode: 'multiCollectionFlow', title: null, description: null, flows: [this.newFlow(task.taskName)] };
        } else if (shape === 'derived') {
            task.pages = null;
            task.summary = { mode: 'derivedCollectionFlow', title: task.taskName, description: null, flows: null, derivedFlows: [this.newDerivedFlow(task.taskName)] };
        } else {
            task.pages = [];
            delete task.summary;
        }
    };

    TemplateDocument.prototype.newDerivedFlow = function (title) {
        return {
            flowId: this.idFor('flow', title),
            title: title,
            description: null,
            sourceFieldId: '',
            sourceType: 'collection',
            fieldId: this.idFor('field', title),
            itemTitleBinding: 'name',
            sectionOrder: 1,
            statusField: 'status',
            pages: []
        };
    };

    TemplateDocument.prototype.newFlow = function (title) {
        return {
            flowId: this.idFor('flow', title),
            title: title,
            fieldId: this.idFor('field', title),
            addButtonLabel: 'Add an item',
            itemKind: 'item',
            itemKindPlural: 'items',
            tableType: 'card',
            pages: []
        };
    };

    TemplateDocument.prototype.newPage = function (title) {
        var pageId = this.idFor('page', title);
        return {
            pageId: pageId,
            slug: kebabCase(title) || pageId,
            title: title,
            description: '',
            pageOrder: 0,
            fields: []
        };
    };

    TemplateDocument.prototype.newField = function (type, label) {
        var field = {
            fieldId: this.idFor('field', label),
            type: type,
            label: { value: label, isVisible: false },
            order: 0,
            validations: []
        };
        if (isChoiceType(type)) field.options = defaultOptions(type);
        if (type === 'complexField') field.complexField = { id: '' };
        return field;
    };

    // ---- Rules made with the rule builder -------------------------------------------------------------------------
    //
    // Anything a rule targets starts hidden, and hide rules run before show rules (lower priority first). A builder
    // rule that shows things is stored as a pair: "<id>-hide" skips the pages and hides the questions when the
    // conditions aren't met, and "<id>-show" shows them when they are. Rules that set or clear answers are one rule,
    // "<id>-set" or "<id>-clear".

    var NEGATED_OPERATORS = {
        equals: 'notEquals',
        notEquals: 'equals',
        'in': 'notIn',
        notIn: 'in',
        isEmpty: 'isNotEmpty',
        isNotEmpty: 'isEmpty',
        greaterThan: 'lessThanOrEqual',
        lessThanOrEqual: 'greaterThan',
        lessThan: 'greaterThanOrEqual',
        greaterThanOrEqual: 'lessThan'
    };

    var VALUELESS_OPERATORS = ['isEmpty', 'isNotEmpty'];

    // What the builder can write: the operator, and the data type it compares as.
    var BUILDER_COMPARISONS = {
        equals: ['string'], notEquals: ['string'], 'in': ['string'], notIn: ['string'],
        isEmpty: ['string'], isNotEmpty: ['string'],
        greaterThan: ['number', 'date'], lessThan: ['number', 'date']
    };

    var RULE_KINDS = { show: '-show', set: '-set', clear: '-clear' };

    function negateOperator(operator) {
        return NEGATED_OPERATORS[operator] || null;
    }

    function ruleTargets(rule, type) {
        return (rule.affectedElements || [])
            .filter(function (element) { return element.elementType === type; })
            .map(function (element) { return element.elementId; });
    }

    function sameValue(a, b) {
        return JSON.stringify(a) === JSON.stringify(b);
    }

    function builderCondition(condition) {
        return {
            triggerField: condition.triggerField,
            operator: condition.operator,
            value: VALUELESS_OPERATORS.indexOf(condition.operator) !== -1 ? '' : condition.value,
            dataType: condition.dataType || 'string'
        };
    }

    function builderGroup(draft) {
        return {
            logicalOperator: draft.match === 'any' && draft.conditions.length > 1 ? 'OR' : 'AND',
            conditions: draft.conditions.map(builderCondition)
        };
    }

    // The group that is true exactly when `group` is false: each condition flipped, and "all" swapped with "any".
    function negateGroup(group) {
        var conditions = group.conditions.map(function (condition) {
            return builderCondition({
                triggerField: condition.triggerField,
                operator: negateOperator(condition.operator),
                value: condition.value,
                dataType: condition.dataType
            });
        });
        var flipped = group.logicalOperator === 'OR' ? 'AND' : 'OR';
        return { logicalOperator: conditions.length > 1 ? flipped : 'AND', conditions: conditions };
    }

    // The conditions of a rule the builder wrote, or null when the rule was written by hand.
    function builderConditions(rule) {
        var group = rule && rule.conditionGroup;
        var conditions = group && group.conditions;
        if (!conditions || !conditions.length) return null;
        if (['AND', 'OR', undefined, null].indexOf(group.logicalOperator) === -1) return null;
        var simple = conditions.every(function (condition) {
            var types = BUILDER_COMPARISONS[condition.operator];
            return condition.triggerField && !(condition.conditions || []).length
                && types && types.indexOf(condition.dataType || 'string') !== -1;
        });
        return simple ? group : null;
    }

    function ruleBase(rule, kind) {
        var suffix = RULE_KINDS[kind];
        if (typeof rule.id !== 'string') return null;
        return rule.id.slice(-suffix.length) === suffix ? rule.id.slice(0, -suffix.length) : rule.id;
    }

    function targetKeys(rule) {
        return (rule.affectedElements || []).map(function (element) { return element.elementType + ':' + element.elementId; }).sort();
    }

    function triggerFields(group) {
        return group.conditions.map(function (condition) { return condition.triggerField; }).sort();
    }

    // A rule that hides exactly what `show` shows, depending on the same answers. Because everything a rule shows
    // starts hidden, the builder can rewrite it as the opposite of the show rule without changing what applicants see.
    function isHideFor(hide, show) {
        var group = builderConditions(hide);
        if (!group || !(hide.affectedElements || []).length) return false;
        var hides = hide.affectedElements.every(function (element) {
            return element.elementType === 'page' ? element.action === 'skip' || element.action === 'hide'
                : element.elementType === 'field' && element.action === 'hide';
        });
        return hides
            && sameValue(targetKeys(hide), targetKeys(show))
            && sameValue(triggerFields(group), triggerFields(show.conditionGroup));
    }

    // Rules the builder can open again: rules that show things (with their matching hide rule, if there is one), and
    // rules that fill in or clear answers. Rules with conditions or actions the builder can't show are left alone.
    TemplateDocument.prototype.builderRules = function () {
        var rules = this.root.conditionalLogic || [];
        var claimed = new Set();
        var entries = [];
        rules.forEach(function (rule) {
            if (claimed.has(rule) || !builderConditions(rule)) return;
            var elements = rule.affectedElements || [];
            if (!elements.length) return;
            var only = function (action) { return elements.every(function (element) { return element.action === action; }); };

            if (only('show')) {
                var hide = rules.find(function (other) { return other !== rule && !claimed.has(other) && isHideFor(other, rule); }) || null;
                claimed.add(rule);
                if (hide) claimed.add(hide);
                entries.push({ base: ruleBase(rule, 'show'), kind: 'show', show: rule, hide: hide, rules: hide ? [hide, rule] : [rule] });
            } else if (only('setValue') && elements.length === 1 && elements[0].elementType === 'field') {
                claimed.add(rule);
                entries.push({ base: ruleBase(rule, 'set'), kind: 'set', show: rule, rules: [rule] });
            } else if (only('clearValue') && elements.every(function (element) { return element.elementType === 'field'; })) {
                claimed.add(rule);
                entries.push({ base: ruleBase(rule, 'clear'), kind: 'clear', show: rule, rules: [rule] });
            }
        });
        return entries;
    };

    TemplateDocument.prototype.builderRuleDraft = function (entry) {
        var group = entry.show.conditionGroup;
        var draft = {
            base: entry.base,
            kind: entry.kind,
            name: entry.show.name || '',
            enabled: entry.show.enabled !== false,
            match: group.logicalOperator === 'OR' ? 'any' : 'all',
            conditions: group.conditions.map(function (condition) {
                return { triggerField: condition.triggerField, operator: condition.operator, value: condition.value, dataType: condition.dataType || 'string' };
            }),
            pages: [],
            fields: [],
            setField: '',
            setValue: '',
            clearFields: []
        };
        if (entry.kind === 'show') {
            draft.pages = ruleTargets(entry.show, 'page');
            draft.fields = ruleTargets(entry.show, 'field');
        } else if (entry.kind === 'set') {
            var element = entry.show.affectedElements[0];
            draft.setField = element.elementId;
            draft.setValue = element.actionConfig && element.actionConfig.value != null ? element.actionConfig.value : '';
        } else {
            draft.clearFields = ruleTargets(entry.show, 'field');
        }
        return draft;
    };

    TemplateDocument.prototype.uniqueRuleBase = function (name, ignoreBase) {
        var ids = new Set((this.root.conditionalLogic || []).map(function (rule) { return rule.id; }));
        var base = kebabCase(name) || 'rule';
        var taken = function (candidate) {
            return ['-show', '-hide', '-set', '-clear'].some(function (suffix) { return ids.has(candidate + suffix); });
        };
        var candidate = base;
        for (var n = 2; candidate !== ignoreBase && taken(candidate); n++) {
            candidate = base + '-' + n;
        }
        return candidate;
    };

    // Writes a builder rule, replacing the rules it was opened from. Returns its base id.
    TemplateDocument.prototype.saveBuilderRule = function (draft) {
        var rules = this.root.conditionalLogic || (this.root.conditionalLogic = []);
        var existing = draft.base
            ? this.builderRules().find(function (entry) { return entry.base === draft.base; })
            : null;
        var kind = draft.kind || 'show';
        var base = existing && existing.kind === kind ? existing.base : this.uniqueRuleBase(draft.name, existing && existing.base);
        var first = existing ? existing.rules[0] : null;
        var priority = first && typeof first.priority === 'number'
            ? first.priority
            : rules.reduce(function (max, rule) { return Math.max(max, typeof rule.priority === 'number' ? rule.priority : 100); }, 90) + 10;
        var enabled = draft.enabled !== false;
        var group = builderGroup(draft);
        // A rule changed without changing what it does keeps its id, priority and any settings the builder doesn't show.
        var same = !!existing && existing.kind === kind;
        var keep = function (rule, changes) { return Object.assign({}, same && rule ? rule : {}, changes); };
        var priorityOf = function (rule, fallback) { return same && rule && typeof rule.priority === 'number' ? rule.priority : fallback; };

        var written;
        if (kind === 'show') {
            var elements = function (pageAction, fieldAction) {
                return (draft.pages || []).map(function (id) { return { elementId: id, elementType: 'page', action: pageAction }; })
                    .concat((draft.fields || []).map(function (id) { return { elementId: id, elementType: 'field', action: fieldAction }; }));
            };
            var oldShow = same ? existing.show : null;
            var oldHide = same ? existing.hide : null;
            written = [];
            if (!same || oldHide) {
                written.push(keep(oldHide, {
                    id: oldHide ? oldHide.id : base + '-hide',
                    name: oldHide && draft.name === oldShow.name ? oldHide.name : 'Hide unless: ' + draft.name,
                    priority: priorityOf(oldHide, priority),
                    enabled: enabled,
                    conditionGroup: negateGroup(group),
                    affectedElements: elements('skip', 'hide')
                }));
            }
            written.push(keep(oldShow, {
                id: oldShow ? oldShow.id : base + '-show',
                name: draft.name,
                priority: priorityOf(oldShow, priority + 1),
                enabled: enabled,
                conditionGroup: group,
                affectedElements: elements('show', 'show')
            }));
        } else {
            written = [keep(same ? existing.show : null, {
                id: same ? existing.show.id : base + RULE_KINDS[kind],
                name: draft.name,
                priority: priorityOf(same ? existing.show : null, priority),
                enabled: enabled,
                conditionGroup: group,
                affectedElements: kind === 'set'
                    ? [{ elementId: draft.setField, elementType: 'field', action: 'setValue', actionConfig: { value: draft.setValue } }]
                    : (draft.clearFields || []).map(function (id) { return { elementId: id, elementType: 'field', action: 'clearValue' }; })
            })];
        }

        var at = existing ? rules.indexOf(existing.rules[0]) : rules.length;
        if (existing) {
            this.root.conditionalLogic = rules = rules.filter(function (rule) { return existing.rules.indexOf(rule) === -1; });
            at = Math.min(at, rules.length);
        }
        rules.splice.apply(rules, [at, 0].concat(written));
        return base;
    };

    TemplateDocument.prototype.removeRules = function (toRemove) {
        if (!this.root.conditionalLogic) return;
        this.root.conditionalLogic = this.root.conditionalLogic.filter(function (rule) { return toRemove.indexOf(rule) === -1; });
    };

    // ---- Reporting safety ------------------------------------------------------------------------------------------
    //
    // Mirrors the API's field compatibility policy: once applications exist, a field keeps its kind and reporting
    // key, and can only leave the template by being listed in retiredFields.

    function fieldKind(field) {
        var type = String(field.type || '').toLowerCase();
        if (['text', 'textarea', 'text-area', 'email', 'character-count'].indexOf(type) !== -1) return 'text';
        if (type === 'radios' || type === 'select') return 'a single choice';
        if (type === 'checkboxes') return 'multiple choices';
        if (type === 'date' || type === 'datetime' || type === 'date-time') return 'a date';
        if (type === 'autocomplete' || type === 'complexfield') return 'a lookup of "' + ((field.complexField && field.complexField.id) || '') + '"';
        return 'a "' + type + '" answer';
    }

    var COLLECTION_KIND = 'a collection of items';

    function fieldKey(parentFieldId, fieldId) {
        return String(parentFieldId || '').toLowerCase() + '|' + String(fieldId || '').toLowerCase();
    }

    // The fields reporting knows about, keyed by collection and field id.
    function inventory(root) {
        var fields = new Map();
        var retired = [];
        function addPageFields(owner, parentFieldId, parentReportingKey) {
            (owner.pages || []).forEach(function (page) {
                (page.fields || []).forEach(function (field) {
                    if (!field.fieldId) return;
                    var semanticKey = field.semanticKey || field.fieldId;
                    var key = fieldKey(parentFieldId, field.fieldId);
                    if (!fields.has(key)) {
                        fields.set(key, {
                            fieldId: field.fieldId,
                            parentFieldId: parentFieldId || '',
                            kind: fieldKind(field),
                            semanticKey: semanticKey,
                            reportingKey: parentReportingKey ? parentReportingKey + '/' + semanticKey : semanticKey
                        });
                    }
                });
            });
        }
        (root.taskGroups || []).forEach(function (group) {
            (group.tasks || []).forEach(function (task) {
                addPageFields(task, '', '');
                var summary = task.summary || {};
                (summary.flows || []).concat(summary.derivedFlows || []).forEach(function (flow) {
                    if (!flow.fieldId) return;
                    var key = fieldKey('', flow.fieldId);
                    var semanticKey = flow.semanticKey || flow.fieldId;
                    if (!fields.has(key)) {
                        fields.set(key, { fieldId: flow.fieldId, parentFieldId: '', kind: COLLECTION_KIND, semanticKey: semanticKey, reportingKey: semanticKey });
                    }
                    addPageFields(flow, flow.fieldId, fields.get(key).reportingKey);
                });
            });
        });
        (root.retiredFields || []).forEach(function (entry) {
            if (entry && entry.fieldId) retired.push(entry);
        });
        return { fields: fields, retired: retired };
    }

    function isRetiredIn(retired, parentFieldId, fieldId) {
        return retired.some(function (entry) {
            return fieldKey(entry.parentFieldId, entry.fieldId) === fieldKey(parentFieldId, fieldId);
        });
    }

    function describeField(field) {
        return '"' + field.fieldId + '"' + (field.parentFieldId ? ' (in the list "' + field.parentFieldId + '")' : '');
    }

    // Problems the API will reject once applications use the template. `baselineRoot` is the version being edited.
    TemplateDocument.prototype.reportingIssues = function (baselineRoot) {
        var before = inventory(baselineRoot);
        var after = inventory(this.root);
        var issues = [];

        before.fields.forEach(function (previous, key) {
            var current = after.fields.get(key);
            if (!current) {
                if (!isRetiredIn(after.retired, previous.parentFieldId, previous.fieldId)
                    && !(previous.parentFieldId && isRetiredIn(after.retired, '', previous.parentFieldId))) {
                    issues.push({
                        fieldId: previous.fieldId,
                        message: 'Question ' + describeField(previous) + ' has been removed but isn\'t retired. Applications already have answers to it, so retire it instead of deleting it.'
                    });
                }
                return;
            }
            if (previous.kind !== current.kind) {
                issues.push({
                    fieldId: current.fieldId,
                    message: 'Question ' + describeField(current) + ' used to store ' + previous.kind + ' but now stores ' + current.kind + '. Give it a new answer id and retire the old one.'
                });
            } else if (previous.semanticKey.toLowerCase() !== current.semanticKey.toLowerCase()) {
                issues.push({
                    fieldId: current.fieldId,
                    message: 'Question ' + describeField(current) + ' has a different reporting key ("' + current.semanticKey + '" instead of "' + previous.semanticKey + '"). Put it back to "' + previous.semanticKey + '".'
                });
            }
        });

        // A new id can only carry on a removed question's reporting key as its declared, like-for-like replacement.
        after.fields.forEach(function (current, key) {
            if (before.fields.has(key)) return;
            var previous = Array.from(before.fields.values()).find(function (field) {
                return field.reportingKey.toLowerCase() === current.reportingKey.toLowerCase()
                    && !after.fields.has(fieldKey(field.parentFieldId, field.fieldId));
            });
            if (!previous) return;
            if (previous.kind !== current.kind) {
                issues.push({
                    fieldId: current.fieldId,
                    message: 'Question ' + describeField(current) + ' keeps the reporting key of ' + describeField(previous) + ', which stored ' + previous.kind
                        + ', but it stores ' + current.kind + '. Give it its own reporting key.'
                });
                return;
            }
            var retirement = after.retired.find(function (entry) {
                return fieldKey(entry.parentFieldId, entry.fieldId) === fieldKey(previous.parentFieldId, previous.fieldId);
            });
            var named = retirement && (retirement.replacedBy || []).some(function (id) { return String(id).toLowerCase() === current.fieldId.toLowerCase(); });
            if (!named && !current.parentFieldId) {
                issues.push({
                    fieldId: current.fieldId,
                    message: 'Question ' + describeField(current) + ' keeps the reporting key of ' + describeField(previous) + ', so the retired "' + previous.fieldId + '" must name it as its replacement.'
                });
            }
        });

        after.retired.forEach(function (entry) {
            if (after.fields.has(fieldKey(entry.parentFieldId, entry.fieldId))) {
                issues.push({
                    fieldId: entry.fieldId,
                    message: 'Question ' + describeField({ fieldId: entry.fieldId, parentFieldId: entry.parentFieldId }) + ' is retired but is still in the template. Remove it from the retired list, or delete the question.'
                });
            }
            (entry.replacedBy || []).forEach(function (replacement) {
                var exists = Array.from(after.fields.values()).some(function (field) { return field.fieldId.toLowerCase() === String(replacement).toLowerCase(); });
                if (!exists) {
                    issues.push({
                        fieldId: entry.fieldId,
                        message: 'Retired question "' + entry.fieldId + '" is replaced by "' + replacement + '", but there is no question with that answer id.'
                    });
                }
            });
        });

        var byReportingKey = new Map();
        after.fields.forEach(function (field) {
            var key = field.reportingKey.toLowerCase();
            byReportingKey.set(key, (byReportingKey.get(key) || []).concat([field]));
            if (field.semanticKey.indexOf('/') !== -1) {
                issues.push({ fieldId: field.fieldId, message: 'Question ' + describeField(field) + ' has a reporting key containing "/", which isn\'t allowed.' });
            }
        });
        byReportingKey.forEach(function (shared) {
            if (shared.length > 1) {
                issues.push({
                    fieldId: shared[0].fieldId,
                    message: 'Questions ' + shared.map(describeField).join(' and ') + ' share the reporting key "' + shared[0].reportingKey + '". Each needs its own.'
                });
            }
        });

        return issues;
    };

    // The reporting identity of a question or list: its id and the list it belongs to.
    TemplateDocument.prototype.fieldKeyOf = function (path) {
        var node = this.get(path);
        var kind = this.kindOf(path);
        if (kind === 'flow' || kind === 'derivedFlow') return { fieldId: node.fieldId, parentFieldId: '' };
        if (kind !== 'field') return null;
        var owner = this.ancestorPath(path, 'flow') || this.ancestorPath(path, 'derivedFlow');
        return { fieldId: node.fieldId, parentFieldId: owner ? this.get(owner).fieldId : '' };
    };

    // Every reporting key under a node. A list counts as one key: retiring a list retires its questions.
    TemplateDocument.prototype.fieldKeysIn = function (path) {
        var self = this;
        var kind = this.kindOf(path);
        if (kind === 'field' || kind === 'flow' || kind === 'derivedFlow') return [this.fieldKeyOf(path)];
        var keys = [];
        (function walk(current) {
            var currentKind = self.kindOf(current);
            if (currentKind === 'field' || currentKind === 'flow' || currentKind === 'derivedFlow') {
                keys.push(self.fieldKeyOf(current));
                return;
            }
            var node = self.get(current);
            var containers = { template: [['taskGroups']], group: [['tasks']], task: [['pages'], ['summary', 'flows'], ['summary', 'derivedFlows']], page: [['fields']] }[currentKind] || [];
            containers.forEach(function (keyPath) {
                var items = keyPath.reduce(function (value, part) { return value && value[part]; }, node);
                (items || []).forEach(function (_, index) { walk(current.concat(keyPath, [index])); });
            });
        })(path);
        return keys;
    };

    TemplateDocument.prototype.retire = function (key, replacedBy) {
        var retired = this.root.retiredFields || (this.root.retiredFields = []);
        var entry = retired.find(function (item) { return fieldKey(item.parentFieldId, item.fieldId) === fieldKey(key.parentFieldId, key.fieldId); });
        if (!entry) {
            entry = { fieldId: key.fieldId };
            if (key.parentFieldId) entry.parentFieldId = key.parentFieldId;
            retired.push(entry);
        }
        (replacedBy || []).forEach(function (id) {
            if (!id) return;
            entry.replacedBy = entry.replacedBy || [];
            if (entry.replacedBy.indexOf(id) === -1) entry.replacedBy.push(id);
        });
        return entry;
    };

    TemplateDocument.prototype.unretire = function (key) {
        if (!this.root.retiredFields) return;
        this.root.retiredFields = this.root.retiredFields.filter(function (item) {
            return fieldKey(item.parentFieldId, item.fieldId) !== fieldKey(key.parentFieldId, key.fieldId);
        });
    };

    function defaultOptions(type) {
        return type === 'radios'
            ? [{ value: 'yes', label: 'Yes' }, { value: 'no', label: 'No' }]
            : [{ value: 'option-1', label: 'Option 1' }, { value: 'option-2', label: 'Option 2' }];
    }

    var api = {
        TemplateDocument: TemplateDocument,
        isChoiceType: isChoiceType,
        defaultOptions: defaultOptions,
        kebabCase: kebabCase,
        camelCase: camelCase,
        pathKey: pathKey,
        parsePathKey: parsePathKey,
        samePath: samePath,
        startsWith: startsWith,
        eachCondition: eachCondition,
        negateOperator: negateOperator,
        fieldKind: fieldKind,
        fieldKey: fieldKey,
        inventory: inventory
    };

    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    } else {
        root.FlexFormsTemplateDocument = api;
    }
})(this);
