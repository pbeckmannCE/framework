﻿/// <reference path="globals.ts"/>
define(["require", "exports", "Framework/Signum.Web/Signum/Scripts/Entities"], function(require, exports, Entities) {
    function cleanError($element) {
        $element.removeClass(exports.inputErrorClass);
    }
    exports.cleanError = cleanError;

    exports.inputErrorClass = "input-validation-error";
    exports.fieldErrorClass = "sf-field-validation-error";
    exports.summaryErrorClass = "validation-summary-errors";
    exports.inlineErrorVal = "inlineVal";
    exports.globalErrorsKey = "sfGlobalErrors";
    exports.globalValidationSummary = "sfGlobalValidationSummary";

    function validate(valOptions) {
        SF.log("validate");

        var options = $.extend({
            prefix: "",
            controllerUrl: SF.Urls.validate,
            requestExtraJsonData: null,
            ajaxError: null,
            errorSummaryId: null
        }, valOptions);

        return SF.ajaxPost({
            url: options.controllerUrl,
            async: false,
            data: constructRequestData(options)
        }).then(function (result) {
            var validatorResult = {
                modelState: result.ModelState,
                isValid: isValid(result.ModelState),
                newToStr: result[Entities.Keys.toStr],
                newLink: result[Entities.Keys.link]
            };
            exports.showErrors(options, validatorResult.modelState);
            return validatorResult;
        });
    }
    exports.validate = validate;

    function constructRequestData(valOptions) {
        SF.log("Validator constructRequestData");

        var formValues = exports.getFormValues(valOptions.prefix, "prefix");

        if (valOptions.rootType)
            formValues["rootType"] = valOptions.rootType;

        if (valOptions.propertyRoute)
            formValues["propertyRoute"] = valOptions.propertyRoute;

        return $.extend(formValues, valOptions.requestExtraJsonData);
    }

    function getFormValues(prefix, prefixRequestKey) {
        var result;
        if (!prefix) {
            result = cleanFormInputs($("form :input")).serializeObject();
        } else {
            var mainControl = $("#{0}_divMainControl".format(prefix));

            result = cleanFormInputs(mainControl.find(":input")).serializeObject();

            result[SF.compose(prefix, Entities.Keys.runtimeInfo)] = mainControl.data("runtimeinfo");

            result = $.extend(result, exports.getFormBasics());
        }

        if (prefixRequestKey)
            result[prefixRequestKey] = prefix;

        return result;
    }
    exports.getFormValues = getFormValues;

    function getFormValuesLite(prefix, prefixRequestKey) {
        var result = exports.getFormBasics();

        result[SF.compose(prefix, Entities.Keys.runtimeInfo)] = prefix ? $("#{0}_divMainControl".format(prefix)).data("runtimeinfo") : $('#' + SF.compose(prefix, Entities.Keys.runtimeInfo)).val();

        if (prefixRequestKey)
            result[prefixRequestKey] = prefix;

        return result;
    }
    exports.getFormValuesLite = getFormValuesLite;

    function getFormValuesHtml(entityHtml, prefixRequestKey) {
        var mainControl = entityHtml.html.find("#{0}_divMainControl".format(entityHtml.prefix));

        var result = cleanFormInputs(mainControl.find(":input")).serializeObject();

        result[SF.compose(entityHtml.prefix, Entities.Keys.runtimeInfo)] = mainControl.data("runtimeinfo");

        if (prefixRequestKey)
            result[prefixRequestKey] = entityHtml.prefix;

        return $.extend(result, exports.getFormBasics());
    }
    exports.getFormValuesHtml = getFormValuesHtml;

    function getFormBasics() {
        return $('#' + Entities.Keys.tabId + ", input:hidden[name=" + Entities.Keys.antiForgeryToken + "]").serializeObject();
    }
    exports.getFormBasics = getFormBasics;

    function cleanFormInputs(form) {
        return form.not(".sf-search-control :input");
    }

    function isModelState(result) {
        return typeof result == "Object" && typeof result.ModelState != "undefined";
    }
    exports.isModelState = isModelState;

    function showErrors(valOptions, modelState) {
        valOptions = $.extend({
            prefix: "",
            showInlineErrors: true,
            fixedInlineErrorText: "*",
            errorSummaryId: null,
            showPathErrors: false
        }, valOptions);

        SF.log("Validator showErrors");

        //Remove previous errors
        $('.' + exports.fieldErrorClass).remove();
        $('.' + exports.inputErrorClass).removeClass(exports.inputErrorClass);
        $('.' + exports.summaryErrorClass).remove();

        var allErrors = [];

        var prefix;
        for (prefix in modelState) {
            if (modelState.hasOwnProperty(prefix)) {
                var errorsArray = modelState[prefix];
                var partialErrors = errorsArray.map(function (a) {
                    return "<li>" + a + "</li>";
                });
                allErrors.push(errorsArray);

                if (prefix != exports.globalErrorsKey && prefix != "") {
                    var $control = $('#' + prefix);
                    $control.addClass(exports.inputErrorClass);
                    $('#' + SF.compose(prefix, Entities.Keys.toStr) + ',#' + SF.compose(prefix, Entities.Keys.link)).addClass(exports.inputErrorClass);
                    if (valOptions.showInlineErrors && $control.hasClass(exports.inlineErrorVal)) {
                        var errorMessage = '<span class="' + exports.fieldErrorClass + '">' + (valOptions.fixedInlineErrorText || errorsArray.join('')) + "</span>";

                        if ($control.next().hasClass("ui-datepicker-trigger"))
                            $control.next().after(errorMessage);
                        else
                            $control.after(errorMessage);
                    }
                }
                setPathErrors(valOptions, prefix, partialErrors.join(''));
            }
        }

        if (allErrors.length) {
            SF.log("(Errors Validator showErrors): " + allErrors.join(''));
            return false;
        }
        return true;
    }
    exports.showErrors = showErrors;

    //This will mark all the path with the error class, and it will also set summary error entries for the controls more inner than the current one
    function setPathErrors(valOptions, prefix, partialErrors) {
        var pathPrefixes = (prefix != exports.globalErrorsKey) ? SF.getPathPrefixes(prefix) : new Array("");
        for (var i = 0, l = pathPrefixes.length; i < l; i++) {
            var currPrefix = pathPrefixes[i];
            if (currPrefix != undefined) {
                var isEqual = (currPrefix === valOptions.prefix);
                var isMoreInner = !isEqual && (currPrefix.indexOf(valOptions.prefix) > -1);
                if (valOptions.showPathErrors || isMoreInner) {
                    $('#' + SF.compose(currPrefix, Entities.Keys.toStr)).addClass(exports.inputErrorClass);
                    $('#' + SF.compose(currPrefix, Entities.Keys.link)).addClass(exports.inputErrorClass);
                }
                if (valOptions.errorSummaryId || ((isMoreInner || isEqual) && $('#' + SF.compose(currPrefix, exports.globalValidationSummary)).length > 0 && !SF.isEmpty(partialErrors))) {
                    var currentSummary = valOptions.errorSummaryId ? $('#' + valOptions.errorSummaryId) : $('#' + SF.compose(currPrefix, exports.globalValidationSummary));

                    var summaryUL = currentSummary.children('.' + exports.summaryErrorClass);
                    if (summaryUL.length === 0) {
                        currentSummary.append('<ul class="' + exports.summaryErrorClass + '">\n' + partialErrors + '</ul>');
                    } else {
                        summaryUL.append(partialErrors);
                    }
                }
            }
        }
    }

    function isValid(modelState) {
        SF.log("Validator isValid");
        for (var prefix in modelState) {
            if (modelState.hasOwnProperty(prefix) && modelState[prefix].length) {
                return false;
            }
        }
        return true;
    }

    function entityIsValid(validationOptions) {
        SF.log("Validator EntityIsValid");

        return exports.validate(validationOptions).then(function (result) {
            if (result.isValid)
                return;

            SF.Notify.error(lang.signum.error, 2000);
            alert(lang.signum.popupErrorsStop);
            throw result;
        });
    }
    exports.entityIsValid = entityIsValid;
});
//# sourceMappingURL=Validator.js.map
