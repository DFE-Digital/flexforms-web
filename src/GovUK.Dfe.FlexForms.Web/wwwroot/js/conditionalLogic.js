/**
 * Client-side conditional logic evaluation system
 * Provides real-time form behavior based on conditional logic rules
 */
class ConditionalLogicEngine {
    constructor() {
        this.rules = [];
        this.formData = {};
        this.fieldStates = {
            visibility: {},
            required: {},
            enabled: {},
            values: {}
        };
        this.debounceTimers = {};
    }

    /**
     * Initialize the conditional logic engine with rules and initial data
     * @param {Array} conditionalLogicRules - Array of conditional logic rules
     * @param {Object} initialFormData - Initial form data
     */
    initialize(conditionalLogicRules, initialFormData = {}) {
        this.rules = conditionalLogicRules || [];
        this.formData = { ...initialFormData };
        
        // Bind event listeners to form fields
        this.bindFormEventListeners();
        
        // Apply initial conditional logic
        this.evaluateAllRules();
        
        console.log(`Conditional Logic Engine initialized with ${this.rules.length} rules`);
    }

    /**
     * Bind event listeners to form fields
     */
    bindFormEventListeners() {
        document.addEventListener('change', (event) => {
            if (event.target.matches('input, select, textarea')) {
                this.handleFieldChange(event.target);
            }
        });

        document.addEventListener('input', (event) => {
            if (event.target.matches('input[type="text"], input[type="email"], input[type="number"], textarea')) {
                this.handleFieldInput(event.target);
            }
        });
    }

    /**
     * Handle field change events
     * @param {HTMLElement} field - The field that changed
     */
    handleFieldChange(field) {
        const fieldId = this.extractFieldId(field);
        if (!fieldId) return;

        this.updateFormData(fieldId, this.getFieldValue(field));
        this.evaluateTriggeredRules(fieldId);
    }

    /**
     * Handle field input events (with debouncing)
     * @param {HTMLElement} field - The field with input
     */
    handleFieldInput(field) {
        const fieldId = this.extractFieldId(field);
        if (!fieldId) return;

        // Clear existing timer
        if (this.debounceTimers[fieldId]) {
            clearTimeout(this.debounceTimers[fieldId]);
        }

        // Set new timer
        this.debounceTimers[fieldId] = setTimeout(() => {
            this.updateFormData(fieldId, this.getFieldValue(field));
            this.evaluateTriggeredRules(fieldId);
        }, 300); // Default debounce time
    }

    /**
     * Extract field ID from form element
     * @param {HTMLElement} field - The form field
     * @returns {string|null} - The field ID or null
     */
    extractFieldId(field) {
        // Check for data-field-id attribute first
        if (field.hasAttribute('data-field-id')) {
            return field.getAttribute('data-field-id');
        }

        // Check for name attribute with Data[fieldId] pattern
        if (field.name && field.name.match(/^Data\[(.+?)\]$/)) {
            const match = field.name.match(/^Data\[(.+?)\]$/);
            return match[1];
        }

        // Check for id attribute
        if (field.id) {
            return field.id;
        }

        return null;
    }

    /**
     * Get the value of a form field
     * @param {HTMLElement} field - The form field
     * @returns {any} - The field value
     */
    getFieldValue(field) {
        if (field.type === 'checkbox') {
            return field.checked;
        } else if (field.type === 'radio') {
            const radioGroup = document.querySelectorAll(`input[name="${field.name}"]`);
            for (const radio of radioGroup) {
                if (radio.checked) {
                    return radio.value;
                }
            }
            return '';
        } else if (field.tagName === 'SELECT' && field.multiple) {
            return Array.from(field.selectedOptions).map(option => option.value);
        } else {
            return field.value;
        }
    }

    /**
     * Update the form data
     * @param {string} fieldId - The field ID
     * @param {any} value - The new value
     */
    updateFormData(fieldId, value) {
        this.formData[fieldId] = value;
    }

    /**
     * Evaluate all conditional logic rules
     */
    evaluateAllRules() {
        const actions = [];

        for (const rule of this.rules) {
            if (!rule.enabled) continue;

            try {
                if (this.evaluateRule(rule)) {
                    actions.push(...rule.affectedElements.map(element => ({
                        element,
                        ruleId: rule.id,
                        priority: rule.priority || 100
                    })));
                }
            } catch (error) {
                console.error(`Error evaluating rule ${rule.id}:`, error);
            }
        }

        // Sort by priority and apply actions
        actions.sort((a, b) => a.priority - b.priority);
        this.applyActions(actions);
    }

    /**
     * Evaluate rules triggered by a specific field
     * @param {string} fieldId - The field that triggered the evaluation
     */
    evaluateTriggeredRules(fieldId) {
        const triggeredRules = this.rules.filter(rule => 
            rule.enabled && this.ruleReferencesField(rule, fieldId)
        );

        const actions = [];

        for (const rule of triggeredRules) {
            try {
                if (this.evaluateRule(rule)) {
                    actions.push(...rule.affectedElements.map(element => ({
                        element,
                        ruleId: rule.id,
                        priority: rule.priority || 100
                    })));
                }
            } catch (error) {
                console.error(`Error evaluating triggered rule ${rule.id}:`, error);
            }
        }

        // Sort by priority and apply actions
        actions.sort((a, b) => a.priority - b.priority);
        this.applyActions(actions);
    }

    /**
     * Check if a rule references a specific field
     * @param {Object} rule - The conditional logic rule
     * @param {string} fieldId - The field ID to check
     * @returns {boolean} - True if the rule references the field
     */
    ruleReferencesField(rule, fieldId) {
        return this.conditionGroupReferencesField(rule.conditionGroup, fieldId);
    }

    /**
     * Check if a condition group references a specific field
     * @param {Object} conditionGroup - The condition group
     * @param {string} fieldId - The field ID to check
     * @returns {boolean} - True if the condition group references the field
     */
    conditionGroupReferencesField(conditionGroup, fieldId) {
        if (!conditionGroup.conditions) return false;

        return conditionGroup.conditions.some(condition => {
            if (condition.triggerField === fieldId) {
                return true;
            }
            if (condition.conditions) {
                return this.conditionGroupReferencesField({ conditions: condition.conditions }, fieldId);
            }
            return false;
        });
    }

    /**
     * Evaluate a single rule
     * @param {Object} rule - The conditional logic rule
     * @returns {boolean} - True if the rule conditions are met
     */
    evaluateRule(rule) {
        return this.evaluateConditionGroup(rule.conditionGroup);
    }

    /**
     * Evaluate a condition group
     * @param {Object} conditionGroup - The condition group
     * @returns {boolean} - True if the condition group is satisfied
     */
    evaluateConditionGroup(conditionGroup) {
        if (!conditionGroup.conditions || conditionGroup.conditions.length === 0) {
            return true;
        }

        const results = conditionGroup.conditions.map(condition => {
            if (condition.conditions) {
                // Nested condition group
                return this.evaluateConditionGroup({
                    logicalOperator: condition.logicalOperator || 'AND',
                    conditions: condition.conditions
                });
            } else {
                return this.evaluateCondition(condition);
            }
        });

        const operator = (conditionGroup.logicalOperator || 'AND').toUpperCase();
        switch (operator) {
            case 'OR':
                return results.some(r => r);
            case 'NOT':
                return !results.every(r => r);
            default: // AND
                return results.every(r => r);
        }
    }

    /**
     * Evaluate a single condition
     * @param {Object} condition - The condition to evaluate
     * @returns {boolean} - True if the condition is satisfied
     */
    evaluateCondition(condition) {
        const fieldValue = this.formData[condition.triggerField];
        const expectedValue = condition.value;
        const operator = condition.operator.toLowerCase();
        const dataType = condition.dataType || 'string';

        switch (operator) {
            case 'equals':
                return this.compareEquals(fieldValue, expectedValue, dataType);
            case 'notequals':
                return !this.compareEquals(fieldValue, expectedValue, dataType);
            case 'in':
                return this.compareIn(fieldValue, expectedValue);
            case 'notin':
                return !this.compareIn(fieldValue, expectedValue);
            case 'contains':
                return this.compareContains(fieldValue, expectedValue);
            case 'startswith':
                return this.compareStartsWith(fieldValue, expectedValue);
            case 'endswith':
                return this.compareEndsWith(fieldValue, expectedValue);
            case 'greaterthan':
                return this.compareGreaterThan(fieldValue, expectedValue, dataType);
            case 'lessthan':
                return this.compareLessThan(fieldValue, expectedValue, dataType);
            case 'greaterthanorequal':
                return this.compareGreaterThanOrEqual(fieldValue, expectedValue, dataType);
            case 'lessthanorequal':
                return this.compareLessThanOrEqual(fieldValue, expectedValue, dataType);
            case 'isempty':
                return this.compareIsEmpty(fieldValue);
            case 'isnotempty':
                return !this.compareIsEmpty(fieldValue);
            case 'istrue':
                return this.compareIsTrue(fieldValue);
            case 'isfalse':
                return !this.compareIsTrue(fieldValue);
            default:
                console.warn(`Unknown operator: ${operator}`);
                return false;
        }
    }

    /**
     * Apply actions to the form
     * @param {Array} actions - Array of actions to apply
     */
    applyActions(actions) {
        // Reset states before applying new actions
        this.resetFieldStates();

        for (const action of actions) {
            try {
                this.applyAction(action);
            } catch (error) {
                console.error(`Error applying action for element ${action.element.elementId}:`, error);
            }
        }

        // Apply the states to the DOM
        this.applyStatesToDOM();
    }

    /**
     * Reset field states to default
     */
    resetFieldStates() {
        // Get all form fields and set default states
        const fields = document.querySelectorAll('input, select, textarea');
        
        fields.forEach(field => {
            const fieldId = this.extractFieldId(field);
            if (fieldId) {
                this.fieldStates.visibility[fieldId] = true;
                this.fieldStates.enabled[fieldId] = true;
                // Don't reset required state as it should come from the original field definition
            }
        });
    }

    /**
     * Apply a single action
     * @param {Object} action - The action to apply
     */
    applyAction(action) {
        const element = action.element;
        const actionType = element.action.toLowerCase();
        const elementId = element.elementId;

        switch (actionType) {
            case 'show':
                this.fieldStates.visibility[elementId] = true;
                break;
            case 'hide':
                this.fieldStates.visibility[elementId] = false;
                break;
            case 'require':
                this.fieldStates.required[elementId] = true;
                break;
            case 'makeoptional':
                this.fieldStates.required[elementId] = false;
                break;
            case 'enable':
                this.fieldStates.enabled[elementId] = true;
                break;
            case 'disable':
                this.fieldStates.enabled[elementId] = false;
                break;
            case 'setvalue':
                if (element.actionConfig && element.actionConfig.value !== undefined) {
                    this.fieldStates.values[elementId] = element.actionConfig.value;
                }
                break;
            case 'clearvalue':
                this.fieldStates.values[elementId] = '';
                break;
            case 'showmessage':
                this.showMessage(element, action.ruleId);
                break;
        }
    }

    /**
     * Apply field states to the DOM
     */
    applyStatesToDOM() {
        // Apply visibility
        Object.entries(this.fieldStates.visibility).forEach(([fieldId, isVisible]) => {
            this.setFieldVisibility(fieldId, isVisible);
        });

        // Apply enabled state
        Object.entries(this.fieldStates.enabled).forEach(([fieldId, isEnabled]) => {
            this.setFieldEnabled(fieldId, isEnabled);
        });

        // Apply required state
        Object.entries(this.fieldStates.required).forEach(([fieldId, isRequired]) => {
            this.setFieldRequired(fieldId, isRequired);
        });

        // Apply values
        Object.entries(this.fieldStates.values).forEach(([fieldId, value]) => {
            this.setFieldValue(fieldId, value);
        });
    }

    /**
     * Set field visibility
     * @param {string} fieldId - The field ID
     * @param {boolean} isVisible - Whether the field should be visible
     */
    setFieldVisibility(fieldId, isVisible) {
        const elements = this.getFieldElements(fieldId);
        elements.forEach(element => {
            const container = this.getFieldContainer(element);
            if (container) {
                container.style.display = isVisible ? '' : 'none';
                container.setAttribute('data-conditional-hidden', !isVisible);
            }
        });
    }

    /**
     * Set field enabled state
     * @param {string} fieldId - The field ID
     * @param {boolean} isEnabled - Whether the field should be enabled
     */
    setFieldEnabled(fieldId, isEnabled) {
        const elements = this.getFieldElements(fieldId);
        elements.forEach(element => {
            element.disabled = !isEnabled;
            element.setAttribute('data-conditional-disabled', !isEnabled);
        });
    }

    /**
     * Set field required state
     * @param {string} fieldId - The field ID
     * @param {boolean} isRequired - Whether the field should be required
     */
    setFieldRequired(fieldId, isRequired) {
        const elements = this.getFieldElements(fieldId);
        elements.forEach(element => {
            element.required = isRequired;
            element.setAttribute('data-conditional-required', isRequired);
            
            // Update visual indicators
            const label = this.getFieldLabel(element);
            if (label) {
                const requiredIndicator = label.querySelector('.required-indicator');
                if (isRequired && !requiredIndicator) {
                    const indicator = document.createElement('span');
                    indicator.className = 'required-indicator';
                    indicator.textContent = ' *';
                    indicator.style.color = 'red';
                    label.appendChild(indicator);
                } else if (!isRequired && requiredIndicator) {
                    requiredIndicator.remove();
                }
            }
        });
    }

    /**
     * Set field value
     * @param {string} fieldId - The field ID
     * @param {any} value - The value to set
     */
    setFieldValue(fieldId, value) {
        const elements = this.getFieldElements(fieldId);
        elements.forEach(element => {
            if (element.type === 'checkbox') {
                element.checked = Boolean(value);
            } else if (element.type === 'radio') {
                element.checked = element.value === String(value);
            } else {
                element.value = String(value);
            }
            
            // Update form data
            this.updateFormData(fieldId, value);
            
            // Trigger change event
            element.dispatchEvent(new Event('change', { bubbles: true }));
        });
    }

    /**
     * Show a message
     * @param {Object} element - The element configuration
     * @param {string} ruleId - The rule ID that triggered this message
     */
    showMessage(element, ruleId) {
        if (!element.actionConfig || !element.actionConfig.message) return;

        const message = element.actionConfig.message;
        const messageType = element.actionConfig.messageType || 'info';
        
        // Create or update message element
        let messageContainer = document.getElementById(`conditional-message-${ruleId}`);
        if (!messageContainer) {
            messageContainer = document.createElement('div');
            messageContainer.id = `conditional-message-${ruleId}`;
            messageContainer.className = `conditional-message alert alert-${messageType}`;
            
            // Insert near the target field if specified, otherwise at the top of the form
            const targetElement = element.elementId ? this.getFieldElements(element.elementId)[0] : null;
            const insertPoint = targetElement ? this.getFieldContainer(targetElement) : document.querySelector('form');
            
            if (insertPoint) {
                insertPoint.parentNode.insertBefore(messageContainer, insertPoint.nextSibling);
            }
        }
        
        messageContainer.textContent = message;
        messageContainer.style.display = 'block';
    }

    /**
     * Get form elements for a field ID
     * @param {string} fieldId - The field ID
     * @returns {NodeList} - The form elements
     */
    getFieldElements(fieldId) {
        // Try multiple selectors to find the field
        const selectors = [
            `[data-field-id="${fieldId}"]`,
            `[name="Data[${fieldId}]"]`,
            `#${fieldId}`,
            `input[name="Data[${fieldId}]"]`,
            `select[name="Data[${fieldId}]"]`,
            `textarea[name="Data[${fieldId}]"]`
        ];

        for (const selector of selectors) {
            const elements = document.querySelectorAll(selector);
            if (elements.length > 0) {
                return elements;
            }
        }

        return document.querySelectorAll(`[data-field-id="${fieldId}"]`);
    }

    /**
     * Get the container element for a field
     * @param {HTMLElement} fieldElement - The field element
     * @returns {HTMLElement|null} - The container element
     */
    getFieldContainer(fieldElement) {
        // Look for common form field container classes
        const containerSelectors = [
            '.govuk-form-group',
            '.form-group',
            '.field-container',
            '.input-group'
        ];

        for (const selector of containerSelectors) {
            const container = fieldElement.closest(selector);
            if (container) {
                return container;
            }
        }

        // Fallback to parent element
        return fieldElement.parentElement;
    }

    /**
     * Get the label element for a field
     * @param {HTMLElement} fieldElement - The field element
     * @returns {HTMLElement|null} - The label element
     */
    getFieldLabel(fieldElement) {
        // Try to find label by 'for' attribute
        if (fieldElement.id) {
            const label = document.querySelector(`label[for="${fieldElement.id}"]`);
            if (label) return label;
        }

        // Try to find label as previous sibling or parent
        const container = this.getFieldContainer(fieldElement);
        return container ? container.querySelector('label') : null;
    }

    // Comparison methods (similar to server-side implementation)

    compareEquals(fieldValue, expectedValue, dataType) {
        if (dataType === 'number') {
            return Number(fieldValue) === Number(expectedValue);
        } else if (dataType === 'boolean') {
            return Boolean(fieldValue) === Boolean(expectedValue);
        } else {
            return String(fieldValue || '').toLowerCase() === String(expectedValue || '').toLowerCase();
        }
    }

    compareIn(fieldValue, expectedValue) {
        if (Array.isArray(expectedValue)) {
            return expectedValue.some(val => 
                String(fieldValue || '').toLowerCase() === String(val || '').toLowerCase()
            );
        } else if (typeof expectedValue === 'string') {
            const values = expectedValue.split(',').map(v => v.trim().toLowerCase());
            return values.includes(String(fieldValue || '').toLowerCase());
        }
        return false;
    }

    compareContains(fieldValue, expectedValue) {
        return String(fieldValue || '').toLowerCase().includes(String(expectedValue || '').toLowerCase());
    }

    compareStartsWith(fieldValue, expectedValue) {
        return String(fieldValue || '').toLowerCase().startsWith(String(expectedValue || '').toLowerCase());
    }

    compareEndsWith(fieldValue, expectedValue) {
        return String(fieldValue || '').toLowerCase().endsWith(String(expectedValue || '').toLowerCase());
    }

    compareGreaterThan(fieldValue, expectedValue, dataType) {
        if (dataType === 'number') {
            return Number(fieldValue) > Number(expectedValue);
        } else if (dataType === 'date') {
            return new Date(fieldValue) > new Date(expectedValue);
        } else {
            return String(fieldValue || '') > String(expectedValue || '');
        }
    }

    compareLessThan(fieldValue, expectedValue, dataType) {
        if (dataType === 'number') {
            return Number(fieldValue) < Number(expectedValue);
        } else if (dataType === 'date') {
            return new Date(fieldValue) < new Date(expectedValue);
        } else {
            return String(fieldValue || '') < String(expectedValue || '');
        }
    }

    compareGreaterThanOrEqual(fieldValue, expectedValue, dataType) {
        return this.compareGreaterThan(fieldValue, expectedValue, dataType) || 
               this.compareEquals(fieldValue, expectedValue, dataType);
    }

    compareLessThanOrEqual(fieldValue, expectedValue, dataType) {
        return this.compareLessThan(fieldValue, expectedValue, dataType) || 
               this.compareEquals(fieldValue, expectedValue, dataType);
    }

    compareIsEmpty(fieldValue) {
        return fieldValue === null || fieldValue === undefined || String(fieldValue).trim() === '';
    }

    compareIsTrue(fieldValue) {
        if (typeof fieldValue === 'boolean') {
            return fieldValue;
        }
        const stringValue = String(fieldValue || '').toLowerCase();
        return stringValue === 'true' || stringValue === 'yes' || stringValue === '1' || stringValue === 'on';
    }
}

// Global instance
window.conditionalLogicEngine = new ConditionalLogicEngine();

// Auto-initialize if rules are provided in the page
document.addEventListener('DOMContentLoaded', () => {
    // Look for conditional logic rules in the page
    const rulesScript = document.getElementById('conditional-logic-rules');
    if (rulesScript && rulesScript.textContent) {
        try {
            const rules = JSON.parse(rulesScript.textContent);
            window.conditionalLogicEngine.initialize(rules);
        } catch (error) {
            console.error('Failed to parse conditional logic rules:', error);
        }
    }

    // Look for initial form data
    const formDataScript = document.getElementById('initial-form-data');
    if (formDataScript && formDataScript.textContent) {
        try {
            const formData = JSON.parse(formDataScript.textContent);
            window.conditionalLogicEngine.initialize(window.conditionalLogicEngine.rules, formData);
        } catch (error) {
            console.error('Failed to parse initial form data:', error);
        }
    }
});
