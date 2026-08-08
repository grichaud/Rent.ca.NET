using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Rent.Web.Infrastructure.ModelBinding;

/// <summary>
/// Parses floating-point form and query values with the invariant culture.
///
/// MVC binds simple types using <see cref="CultureInfo.CurrentCulture"/>, which under the
/// French routes of this app expects a decimal comma. But an <c>&lt;input type="number"&gt;</c>
/// always submits a "valid floating-point number" per the HTML spec — i.e. "1.5" with a
/// point, whatever the page language — and query strings are invariant too. The mismatch
/// silently bound such values to null: a French renter setting "min bathrooms 1.5" had the
/// filter dropped without any validation error.
///
/// Only floating-point types are intercepted. Integers are left to the default binder.
/// </summary>
public class InvariantNumericModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueResult == ValueProviderResult.None) return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);

        var raw = valueResult.FirstValue;
        var targetType = bindingContext.ModelMetadata.UnderlyingOrModelType;
        var isNullable = Nullable.GetUnderlyingType(bindingContext.ModelType) is not null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            // An empty optional field means "no filter", not a validation error.
            if (isNullable) bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        const NumberStyles Styles = NumberStyles.Float | NumberStyles.AllowThousands;
        object? parsed = null;
        var ok = false;

        if (targetType == typeof(decimal))
        {
            ok = decimal.TryParse(raw, Styles, CultureInfo.InvariantCulture, out var d);
            parsed = d;
        }
        else if (targetType == typeof(double))
        {
            ok = double.TryParse(raw, Styles, CultureInfo.InvariantCulture, out var d);
            parsed = d;
        }
        else if (targetType == typeof(float))
        {
            ok = float.TryParse(raw, Styles, CultureInfo.InvariantCulture, out var f);
            parsed = f;
        }

        if (ok)
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"'{raw}' is not a valid number.");
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Applies <see cref="InvariantNumericModelBinder"/> to decimal, double and float
/// (including their nullable forms).
/// </summary>
public class InvariantNumericModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var type = context.Metadata.UnderlyingOrModelType;
        return type == typeof(decimal) || type == typeof(double) || type == typeof(float)
            ? new InvariantNumericModelBinder()
            : null;
    }
}
