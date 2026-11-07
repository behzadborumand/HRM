using HRMS.Web.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HRMS.Web.ModelBinding
{
    /// <summary>
    /// Binds the Jalali text entered in the Persian forms (and ISO Gregorian values, so
    /// links and API-style posts keep working) into DateTime.
    /// </summary>
    public class JalaliDateModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var modelName = bindingContext.ModelName;
            var attempted = bindingContext.ValueProvider.GetValue(modelName).FirstValue;

            if (string.IsNullOrWhiteSpace(attempted))
            {
                if (bindingContext.ModelType == typeof(DateTime))
                {
                    bindingContext.ModelState.AddModelError(modelName, $"«{bindingContext.ModelMetadata.DisplayName ?? modelName}» الزامی است.");
                }

                return Task.CompletedTask;
            }

            if (!PersianDate.TryParse(attempted, out var date))
            {
                bindingContext.ModelState.AddModelError(modelName, "تاریخ واردشده معتبر نیست؛ نمونه: ۱۴۰۵/۰۴/۰۱");
                return Task.CompletedTask;
            }

            bindingContext.Result = ModelBindingResult.Success(
                bindingContext.ModelType == typeof(DateTime?) ? (object?)date : date);
            return Task.CompletedTask;
        }
    }

    public class JalaliDateModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context) =>
            context.Metadata.ModelType == typeof(DateTime) || context.Metadata.ModelType == typeof(DateTime?)
                ? new JalaliDateModelBinder()
                : null;
    }
}
