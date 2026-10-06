// Adds a "Format JSON" button to textareas marked with data-json-format. The button re-indents valid JSON, or
// reports where the JSON stops being valid and puts the cursor there.
(function () {
    function lineAndColumn(text, position) {
        var before = text.slice(0, position).split('\n');
        return { line: before.length, column: before[before.length - 1].length + 1 };
    }

    // Browsers word JSON.parse errors differently: Chromium gives a character position, Firefox a line and column.
    function errorLocation(text, message) {
        var lineColumn = /line (\d+) column (\d+)/i.exec(message);
        if (lineColumn) {
            var line = Number(lineColumn[1]);
            var column = Number(lineColumn[2]);
            var lines = text.split('\n');
            var position = 0;
            for (var i = 0; i < line - 1 && i < lines.length; i++) {
                position += lines[i].length + 1;
            }
            return { line: line, column: column, position: position + column - 1 };
        }

        var at = /position (\d+)/i.exec(message);
        if (at) {
            var location = lineAndColumn(text, Number(at[1]));
            location.position = Number(at[1]);
            return location;
        }

        return null;
    }

    function bind(textarea) {
        var button = document.querySelector('[data-json-format-button="' + textarea.id + '"]');
        var status = document.getElementById(textarea.id + '-format-status');
        if (!button || !status) {
            return;
        }

        button.hidden = false;
        button.addEventListener('click', function () {
            var text = textarea.value;
            try {
                textarea.value = JSON.stringify(JSON.parse(text), null, 2);
                status.className = 'govuk-body govuk-!-margin-top-2';
                status.textContent = 'JSON formatted.';
            } catch (e) {
                var location = errorLocation(text, e.message);
                status.className = 'govuk-error-message govuk-!-margin-top-2';
                status.textContent = location
                    ? 'The JSON isn\'t valid at line ' + location.line + ', column ' + location.column +
                      '. Check for a missing or extra comma, quote or bracket there or just before it.'
                    : 'The JSON isn\'t valid. Check for a missing or extra comma, quote or bracket.';
                if (location) {
                    textarea.focus();
                    textarea.setSelectionRange(location.position, Math.min(location.position + 1, text.length));
                }
            }
        });
    }

    function init() {
        document.querySelectorAll('textarea[data-json-format]').forEach(bind);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
