// Tests for Template Studio's document model. Run with: node --test
// TemplateStudioScriptTests runs them as part of dotnet test.
const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');

const lib = require(path.join(__dirname, '..', '..', '..', 'GovUK.Dfe.FlexForms.Web', 'wwwroot', 'js', 'template-studio', 'template-document.js'));

function template() {
    return {
        templateId: 'tpl',
        templateName: 'Transfers',
        description: '',
        semanticKeyKeptHere: { anything: true },
        taskGroups: [{
            groupId: 'about', groupName: 'About', groupOrder: 1, groupStatus: 'NotStarted',
            tasks: [{
                taskId: 'trust', taskName: 'Trust', taskOrder: 1, taskStatus: 'NotStarted',
                pages: [
                    { pageId: 'has-trust', slug: 'has-trust', title: 'Is there a trust?', description: '', pageOrder: 1,
                        fields: [{ fieldId: 'hasTrust', type: 'radios', order: 1, label: { value: 'Is there a trust?' },
                            options: [{ value: 'yes', label: 'Yes' }, { value: 'no', label: 'No' }] }] },
                    { pageId: 'trust-name', slug: 'trust-name', title: 'Trust name', description: '', pageOrder: 2,
                        fields: [{ fieldId: 'trustName', type: 'text', order: 1, label: { value: 'Trust name' }, semanticKey: 'TrustName' }] }
                ]
            }, {
                taskId: 'academies', taskName: 'Academies', taskOrder: 2, taskStatus: 'NotStarted', pages: null,
                summary: { mode: 'multiCollectionFlow', flows: [{
                    flowId: 'academies-flow', title: 'Academies', fieldId: 'academies',
                    pages: [{ pageId: 'academy', slug: 'academy', title: 'Academy', description: '', pageOrder: 1,
                        fields: [{ fieldId: 'academyName', type: 'text', order: 1, label: { value: 'Academy name' } }] }]
                }] }
            }]
        }],
        conditionalLogic: []
    };
}

function load(root) {
    return lib.TemplateDocument.parse(JSON.stringify(root || template()));
}

const TRUST_PAGES = ['taskGroups', 0, 'tasks', 0, 'pages'];

test('keeps properties Template Studio does not know about', () => {
    const doc = load();
    doc.set(['taskGroups', 0], 'groupName', 'About the trust');
    const saved = JSON.parse(doc.toJson());
    assert.deepEqual(saved.semanticKeyKeptHere, { anything: true });
    assert.equal(saved.taskGroups[0].tasks[0].pages[1].fields[0].semanticKey, 'TrustName');
});

test('rejects JSON that is not an object', () => {
    assert.throws(() => lib.TemplateDocument.parse('[1, 2]'), /must be a JSON object/);
});

test('works out what kind of thing a path points at', () => {
    const doc = load();
    assert.equal(doc.kindOf([]), 'template');
    assert.equal(doc.kindOf(TRUST_PAGES.concat([0])), 'page');
    assert.equal(doc.kindOf(TRUST_PAGES.concat([0, 'fields', 0])), 'field');
    assert.equal(doc.kindOf(['taskGroups', 0, 'tasks', 1, 'summary', 'flows', 0]), 'flow');
    assert.deepEqual(doc.parentPath(['taskGroups', 0, 'tasks', 1, 'summary', 'flows', 0]), ['taskGroups', 0, 'tasks', 1]);
});

test('keeps order numbers equal to positions when pages move', () => {
    const doc = load();
    const moved = doc.move(TRUST_PAGES.concat([1]), -1);
    assert.deepEqual(moved, TRUST_PAGES.concat([0]));
    const pages = doc.get(TRUST_PAGES);
    assert.deepEqual(pages.map(page => [page.pageId, page.pageOrder]), [['trust-name', 1], ['has-trust', 2]]);
});

test('moves a question to another page and renumbers both pages', () => {
    const doc = load();
    const newPath = doc.moveTo(TRUST_PAGES.concat([1, 'fields', 0]), TRUST_PAGES.concat([0, 'fields']));
    assert.deepEqual(newPath, TRUST_PAGES.concat([0, 'fields', 1]));
    assert.deepEqual(doc.get(TRUST_PAGES.concat([0, 'fields'])).map(field => [field.fieldId, field.order]), [['hasTrust', 1], ['trustName', 2]]);
    assert.deepEqual(doc.get(TRUST_PAGES.concat([1, 'fields'])), []);
});

test('duplicates a page with new ids for it and its questions', () => {
    const doc = load();
    const copyPath = doc.duplicate(TRUST_PAGES.concat([1]));
    const copy = doc.get(copyPath);
    assert.equal(copy.pageId, 'trust-name-copy');
    assert.equal(copy.title, 'Trust name (copy)');
    assert.equal(copy.fields[0].fieldId, 'trustNameCopy');
    assert.equal(doc.get(TRUST_PAGES).length, 3);
});

test('makes ids from names that no other node uses', () => {
    const doc = load();
    assert.equal(doc.idFor('field', 'Trust name'), 'trustName-2');
    assert.equal(doc.idFor('page', 'Trust name'), 'trust-name-page');
    assert.equal(doc.idFor('field', 'Trust name', 'trustName'), 'trustName');
    assert.equal(lib.kebabCase('Héllo, World!'), 'hello-world');
    assert.equal(lib.camelCase('What is the trust called?'), 'whatIsTheTrustCalled');
});

test('finds nodes by id, preferring questions over pages', () => {
    const doc = load();
    assert.deepEqual(doc.findById('trustName'), TRUST_PAGES.concat([1, 'fields', 0]));
    assert.deepEqual(doc.findById('academies-flow'), ['taskGroups', 0, 'tasks', 1, 'summary', 'flows', 0]);
    assert.deepEqual(doc.findByName('Trust name'), TRUST_PAGES.concat([1]));
});

test('renames rule references when an id changes', () => {
    const doc = load();
    doc.saveBuilderRule({ kind: 'show', name: 'Trust', match: 'all', conditions: [{ triggerField: 'hasTrust', operator: 'equals', value: 'yes' }], pages: ['trust-name'], fields: [] });
    assert.equal(doc.renameReferences('hasTrust', 'isThereATrust'), 2);
    assert.equal(doc.rulesReferencing('hasTrust').length, 0);
    assert.equal(doc.rulesReferencing('isThereATrust').length, 2);
});

test('rejects renaming onto a retired answer id and restores the field id', () => {
    const doc = load();
    const path = TRUST_PAGES.concat([1, 'fields', 0]);
    doc.retire({ fieldId: 'oldName', parentFieldId: '' });
    doc.get(path).fieldId = 'oldName';
    const message = doc.applyAnswerIdRename(path, 'trustName', 'oldName', { inUse: true, baselineRoot: template() });
    assert.match(message, /already retired/);
    assert.equal(doc.get(path).fieldId, 'trustName');
});

test('renameReferences rewrites every matching id, including page targets that already used the new id', () => {
    const doc = load();
    doc.saveBuilderRule({
        kind: 'show', name: 'Ask', match: 'all',
        conditions: [{ triggerField: 'hasTrust', operator: 'equals', value: 'yes' }],
        pages: ['trust-name'], fields: []
    });
    doc.renameReferences('hasTrust', 'trust-name');
    doc.renameReferences('trust-name', 'hasTrust');
    const show = doc.root.conditionalLogic.find(rule => rule.affectedElements.some(element => element.action === 'show'));
    assert.equal(show.conditionGroup.conditions[0].triggerField, 'hasTrust');
    assert.equal(show.affectedElements[0].elementId, 'hasTrust');
});

test('writes a show rule as a hide and show pair that the builder can open again', () => {
    const doc = load();
    const base = doc.saveBuilderRule({ kind: 'show', name: 'Ask for the trust name', match: 'all', conditions: [{ triggerField: 'hasTrust', operator: 'equals', value: 'yes' }], pages: ['trust-name'], fields: [] });
    assert.equal(base, 'ask-for-the-trust-name');
    const [hide, show] = doc.root.conditionalLogic;
    assert.equal(hide.id, base + '-hide');
    assert.deepEqual(hide.conditionGroup, { logicalOperator: 'AND', conditions: [{ triggerField: 'hasTrust', operator: 'notEquals', value: 'yes', dataType: 'string' }] });
    assert.deepEqual(hide.affectedElements, [{ elementId: 'trust-name', elementType: 'page', action: 'skip' }]);
    assert.ok(show.priority > hide.priority);

    const entries = doc.builderRules();
    assert.equal(entries.length, 1);
    const draft = doc.builderRuleDraft(entries[0]);
    assert.equal(draft.kind, 'show');
    assert.deepEqual(draft.pages, ['trust-name']);
    assert.equal(draft.conditions[0].value, 'yes');
});

test('flips each condition and swaps any for all in the hide rule', () => {
    const doc = load();
    doc.saveBuilderRule({
        kind: 'show', name: 'Big', match: 'any',
        conditions: [
            { triggerField: 'pupils', operator: 'greaterThan', value: '10', dataType: 'number' },
            { triggerField: 'hasTrust', operator: 'in', value: ['yes'] },
            { triggerField: 'trustName', operator: 'isNotEmpty', value: 'ignored' }
        ],
        pages: [], fields: ['trustName']
    });
    const [hide, show] = doc.root.conditionalLogic;
    assert.equal(show.conditionGroup.logicalOperator, 'OR');
    assert.equal(hide.conditionGroup.logicalOperator, 'AND');
    assert.deepEqual(hide.conditionGroup.conditions.map(condition => condition.operator), ['lessThanOrEqual', 'notIn', 'isEmpty']);
    assert.equal(show.conditionGroup.conditions[2].value, '');
    assert.equal(doc.builderRules()[0].kind, 'show');
});

test('leaves rules written by hand alone', () => {
    const root = template();
    root.conditionalLogic = [
        { id: 'custom-show', conditionGroup: { logicalOperator: 'AND', conditions: [{ triggerField: 'a', operator: 'contains', value: 'x' }] }, affectedElements: [{ elementId: 'p', elementType: 'page', action: 'show' }] },
        { id: 'custom-hide', conditionGroup: { logicalOperator: 'AND', conditions: [{ triggerField: 'a', operator: 'equals', value: 'x' }] }, affectedElements: [{ elementId: 'p', elementType: 'page', action: 'skip' }] }
    ];
    assert.deepEqual(load(root).builderRules(), []);
});

test('opens hand-written show and hide pairs in the builder, whatever their ids', () => {
    const root = template();
    root.conditionalLogic = [
        { id: 'hide-trust-name-for-no', name: 'Hide trust name', priority: 60, enabled: true, executeOn: ['change'],
            conditionGroup: { logicalOperator: 'AND', conditions: [{ triggerField: 'hasTrust', operator: 'equals', value: 'no', dataType: 'string', logicalOperator: null, conditions: null }] },
            affectedElements: [{ elementId: 'trust-name', elementType: 'page', action: 'skip', actionConfig: null }, { elementId: 'trustName', elementType: 'field', action: 'hide', actionConfig: null }] },
        { id: 'show-trust-name-for-yes', name: 'Show trust name', priority: 61, enabled: true, executeOn: ['change'],
            conditionGroup: { logicalOperator: 'AND', conditions: [{ triggerField: 'hasTrust', operator: 'equals', value: 'yes', dataType: 'string', logicalOperator: null, conditions: null }] },
            affectedElements: [{ elementId: 'trust-name', elementType: 'page', action: 'show', actionConfig: null }, { elementId: 'trustName', elementType: 'field', action: 'show', actionConfig: null }] }
    ];
    const doc = load(root);
    const [entry] = doc.builderRules();
    assert.equal(entry.kind, 'show');
    assert.equal(entry.hide.id, 'hide-trust-name-for-no');

    const draft = doc.builderRuleDraft(entry);
    draft.conditions[0].value = 'yes';
    const base = doc.saveBuilderRule(draft);
    assert.equal(base, 'show-trust-name-for-yes');
    const [hide, show] = doc.root.conditionalLogic;
    assert.deepEqual([hide.id, hide.name, hide.priority, show.id, show.priority], ['hide-trust-name-for-no', 'Hide trust name', 60, 'show-trust-name-for-yes', 61]);
    assert.deepEqual(show.executeOn, ['change']);
    assert.equal(hide.conditionGroup.conditions[0].operator, 'notEquals');
    assert.equal(doc.builderRules().length, 1);
});

test('opens a show rule without a hide rule, and saves it without adding one', () => {
    const root = template();
    root.conditionalLogic = [{ id: 'show-name', name: 'Show name', priority: 5,
        conditionGroup: { logicalOperator: 'AND', conditions: [{ triggerField: 'hasTrust', operator: 'equals', value: 'yes' }] },
        affectedElements: [{ elementId: 'trustName', elementType: 'field', action: 'show' }] }];
    const doc = load(root);
    const [entry] = doc.builderRules();
    assert.equal(entry.hide, null);
    doc.saveBuilderRule(doc.builderRuleDraft(entry));
    assert.deepEqual(doc.root.conditionalLogic.map(rule => rule.id), ['show-name']);
});

test('writes rules that fill in and clear answers', () => {
    const doc = load();
    const setBase = doc.saveBuilderRule({ kind: 'set', name: 'No trust name', match: 'all', conditions: [{ triggerField: 'hasTrust', operator: 'equals', value: 'no' }], setField: 'trustName', setValue: 'None' });
    const clearBase = doc.saveBuilderRule({ kind: 'clear', name: 'Clear it', match: 'all', conditions: [{ triggerField: 'hasTrust', operator: 'equals', value: 'no' }], clearFields: ['trustName'] });
    const rules = doc.root.conditionalLogic;
    assert.deepEqual(rules.map(rule => rule.id), [setBase + '-set', clearBase + '-clear']);
    assert.deepEqual(rules[0].affectedElements, [{ elementId: 'trustName', elementType: 'field', action: 'setValue', actionConfig: { value: 'None' } }]);
    assert.deepEqual(rules[1].affectedElements, [{ elementId: 'trustName', elementType: 'field', action: 'clearValue' }]);
    assert.deepEqual(doc.builderRules().map(entry => entry.kind), ['set', 'clear']);
});

test('replaces the rules a builder rule was opened from, with a new id when its kind changes', () => {
    const doc = load();
    const base = doc.saveBuilderRule({ kind: 'show', name: 'Ask', match: 'all', conditions: [{ triggerField: 'hasTrust', operator: 'equals', value: 'yes' }], pages: ['trust-name'], fields: [] });
    const draft = doc.builderRuleDraft(doc.builderRules()[0]);
    draft.kind = 'clear';
    draft.name = 'Clear the name';
    draft.clearFields = ['trustName'];
    const newBase = doc.saveBuilderRule(draft);
    assert.notEqual(newBase, base);
    assert.deepEqual(doc.root.conditionalLogic.map(rule => rule.id), ['clear-the-name-clear']);
});

test('reports questions removed without retiring them once applications have answers', () => {
    const baseline = template();
    const doc = load();
    doc.remove(TRUST_PAGES.concat([1]));
    const issues = doc.reportingIssues(baseline);
    assert.equal(issues.length, 1);
    assert.match(issues[0].message, /"trustName" has been removed but isn't retired/);

    doc.retire({ fieldId: 'trustName', parentFieldId: '' });
    assert.deepEqual(doc.reportingIssues(baseline), []);
    doc.unretire({ fieldId: 'trustName', parentFieldId: '' });
    assert.equal(doc.reportingIssues(baseline).length, 1);
});

test('reports a question that changes what kind of answer it stores', () => {
    const baseline = template();
    const doc = load();
    doc.get(TRUST_PAGES.concat([0, 'fields', 0])).type = 'checkboxes';
    const [issue] = doc.reportingIssues(baseline);
    assert.match(issue.message, /used to store a single choice but now stores multiple choices/);
});

test('reports a changed reporting key', () => {
    const baseline = template();
    const doc = load();
    doc.get(TRUST_PAGES.concat([1, 'fields', 0])).semanticKey = 'Name';
    const [issue] = doc.reportingIssues(baseline);
    assert.match(issue.message, /different reporting key/);
});

test('tells the author a retired answer id cannot be put back on a question', () => {
    const baseline = template();
    const doc = load();
    doc.remove(TRUST_PAGES.concat([1]));
    doc.retire({ fieldId: 'trustName', parentFieldId: '' });
    doc.get(TRUST_PAGES.concat([0, 'fields', 0])).fieldId = 'trustName';
    const issues = doc.reportingIssues(baseline);
    assert.match(issues.map(issue => issue.message).join('\n'), /"trustName" is already retired/);
});

test('does not treat swapped answer ids as a removal', () => {
    const baseline = template();
    const doc = load();
    doc.get(TRUST_PAGES.concat([0, 'fields', 0])).fieldId = 'trustName';
    doc.get(TRUST_PAGES.concat([1, 'fields', 0])).fieldId = 'hasTrust';
    const issues = doc.reportingIssues(baseline);
    assert.equal(issues.some(issue => /removed but isn't retired/.test(issue.message)), false);
    assert.equal(issues.some(issue => /already retired/.test(issue.message)), false);
});

test('does not retire an answer id that is still live on another question', () => {
    const baseline = template();
    const doc = load();
    const hasTrustPath = TRUST_PAGES.concat([0, 'fields', 0]);
    const trustNamePath = TRUST_PAGES.concat([1, 'fields', 0]);
    doc.get(hasTrustPath).fieldId = 'trustName';
    doc.get(trustNamePath).fieldId = 'hasTrust';
    const note = doc.applyAnswerIdRename(trustNamePath, 'trustName', 'hasTrust', { inUse: true, baselineRoot: baseline });
    assert.equal(note, '');
    assert.deepEqual(doc.root.retiredFields || [], []);
    assert.equal(doc.get(trustNamePath).semanticKey, 'TrustName');
});

test('retires an answer id that is no longer live once applications use the template', () => {
    const baseline = template();
    const doc = load();
    const path = TRUST_PAGES.concat([1, 'fields', 0]);
    doc.get(path).fieldId = 'trustTitle';
    const note = doc.applyAnswerIdRename(path, 'trustName', 'trustTitle', { inUse: true, baselineRoot: baseline });
    assert.match(note, /retired and replaced by "trustTitle"/);
    assert.deepEqual(doc.root.retiredFields, [{ fieldId: 'trustName', replacedBy: ['trustTitle'] }]);
    assert.equal(doc.get(path).semanticKey, 'TrustName');
});

test('keeps a custom reporting key when an answer is renamed back to its original id', () => {
    const baseline = template();
    const doc = load();
    const path = TRUST_PAGES.concat([1, 'fields', 0]);
    doc.get(path).fieldId = 'trustTitle';
    doc.applyAnswerIdRename(path, 'trustName', 'trustTitle', { inUse: true, baselineRoot: baseline });
    doc.get(path).fieldId = 'trustName';
    doc.applyAnswerIdRename(path, 'trustTitle', 'trustName', { inUse: true, baselineRoot: baseline });
    assert.equal(doc.get(path).semanticKey, 'TrustName');
    assert.deepEqual(doc.root.retiredFields, []);
});

test('drops a reporting key that only repeated the restored answer id', () => {
    const baseline = template();
    const doc = load();
    const path = TRUST_PAGES.concat([0, 'fields', 0]);
    doc.get(path).fieldId = 'hasTrustNow';
    doc.applyAnswerIdRename(path, 'hasTrust', 'hasTrustNow', { inUse: true, baselineRoot: baseline });
    assert.equal(doc.get(path).semanticKey, 'hasTrust');
    doc.get(path).fieldId = 'hasTrust';
    doc.applyAnswerIdRename(path, 'hasTrustNow', 'hasTrust', { inUse: true, baselineRoot: baseline });
    assert.equal(doc.get(path).semanticKey, undefined);
    assert.deepEqual(doc.root.retiredFields, []);
});

test('treats questions in a repeatable list as belonging to the list', () => {
    const doc = load();
    const flowPath = ['taskGroups', 0, 'tasks', 1, 'summary', 'flows', 0];
    assert.deepEqual(doc.fieldKeyOf(flowPath.concat(['pages', 0, 'fields', 0])), { fieldId: 'academyName', parentFieldId: 'academies' });
    assert.deepEqual(doc.fieldKeysIn(['taskGroups', 0, 'tasks', 1]), [{ fieldId: 'academies', parentFieldId: '' }]);
});

test('lists event mappings whose answers are no longer in the template', () => {
    const doc = load();
    const references = [
        { fieldId: 'trustname', eventType: 'Submitted', mappingId: 'm1' },
        { fieldId: 'academies', eventType: 'Submitted', mappingId: 'm1' },
        { fieldId: 'oldQuestion', eventType: 'Submitted', mappingId: 'm1' }
    ];
    assert.deepEqual(doc.missingMappedFields(references).map(reference => reference.fieldId), ['oldQuestion']);
    doc.remove(TRUST_PAGES.concat([1]));
    assert.deepEqual(doc.missingMappedFields(references).map(reference => reference.fieldId), ['trustname', 'oldQuestion']);
});

test('changes a task to a repeatable list only while it is empty', () => {
    const doc = load();
    const task = doc.get(['taskGroups', 0, 'tasks', 0]);
    assert.equal(doc.isTaskEmpty(task), false);
    const fresh = doc.newTask('New task');
    assert.equal(doc.taskShape(fresh), 'pages');
    doc.setTaskShape(fresh, 'collection');
    assert.equal(doc.taskShape(fresh), 'collection');
    assert.equal(fresh.summary.flows.length, 1);
    assert.equal(doc.isTaskEmpty(fresh), true);
});

test('numbers repeated pages in the order they are listed', () => {
    const doc = load();
    const task = doc.newTask('Declarations', 'derived');
    const taskPath = doc.insert(['taskGroups', 0, 'tasks'], task);
    const listPath = taskPath.concat(['summary', 'derivedFlows']);
    doc.insert(listPath, doc.newDerivedFlow('Second'));
    doc.move(listPath.concat([1]), -1);
    assert.deepEqual(doc.get(listPath).map(flow => [flow.title, flow.sectionOrder]), [['Second', 1], ['Declarations', 2]]);
});
