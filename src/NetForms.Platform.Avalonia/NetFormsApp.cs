using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Styling;

namespace NetForms.Platform.Avalonia;

/// <summary>
/// The Avalonia Application behind every NetForms process. No theme package is loaded: the
/// only templated control we use is Window, and its template is a bare ContentPresenter so
/// our surface fills the client area.
/// </summary>
internal sealed class NetFormsApp : Application
{
    public override void Initialize()
    {
        var template = new FuncControlTemplate<Window>((window, scope) =>
        {
            var presenter = new ContentPresenter { Name = "PART_ContentPresenter" };
            presenter.Bind(ContentPresenter.ContentProperty, window.GetObservable(ContentControl.ContentProperty));
            presenter.RegisterInNameScope(scope);
            return presenter;
        });

        var windowStyle = new Style(x => x.OfType<Window>());
        windowStyle.Setters.Add(new Setter(TemplatedControl.TemplateProperty, template));
        windowStyle.Setters.Add(new Setter(TemplatedControl.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0))));
        Styles.Add(windowStyle);
    }
}
