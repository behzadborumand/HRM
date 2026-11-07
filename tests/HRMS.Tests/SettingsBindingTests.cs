using System.Globalization;
using HRMS.Web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HRMS.Tests;

public class SettingsBindingTests
{
    [Fact]
    public async Task Policy_form_with_omitted_checkbox_does_not_reactivate_policy()
        => Assert.False((await Bind<PolicyAdminViewModel>()).Active);

    [Fact]
    public async Task User_form_with_omitted_checkbox_does_not_reactivate_user()
        => Assert.False((await Bind<UserAdminViewModel>()).IsActive);

    private static async Task<T> Bind<T>()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore();
        using var provider = services.BuildServiceProvider();
        var metadata = provider.GetRequiredService<IModelMetadataProvider>().GetMetadataForType(typeof(T));
        var binder = provider.GetRequiredService<IModelBinderFactory>().CreateBinder(new() { Metadata = metadata, CacheToken = typeof(T) });
        var action = new ActionContext(new DefaultHttpContext { RequestServices = provider }, new RouteData(), new ActionDescriptor());
        var values = new FormValueProvider(BindingSource.Form, new FormCollection(new()), CultureInfo.InvariantCulture);
        var context = DefaultModelBindingContext.CreateBindingContext(action, values, metadata, null, string.Empty);
        await binder.BindModelAsync(context);
        return Assert.IsType<T>(context.Result.Model);
    }
}
