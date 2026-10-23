// In-memory form template for the visual editor. It edits the raw JSON so properties the editor doesn't know
// about (semanticKey, retiredFields, ...) are saved unchanged. Nodes are addressed by paths such as
// ['taskGroups', 0, 'tasks', 1, 'pages', 0, 'fields', 2].
(function (root) {
    'use strict';

    // The engine sorts by these and also walks array order, so they're kept equal to the position.
    var ORDER_KEYS = { taskGroups: 'groupOrder', tasks: 'taskOrder', pages: 'pageOrder', fields: 'order' };

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
        if (shape === 'collection') {
            task.pages = null;
            task.summary = { mode: 'multiCollectionFlow', title: null, description: null, flows: [this.newFlow(name)] };
        } else {
            task.pages = [];
        }
        return task;
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
        samePath: samePath,
        startsWith: startsWith,
        eachCondition: eachCondition
    };

    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    } else {
        root.FlexFormsTemplateDocument = api;
    }
})(this);
