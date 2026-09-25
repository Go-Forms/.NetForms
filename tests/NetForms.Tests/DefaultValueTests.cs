using System.ComponentModel;
using System.Reflection;
using NetForms.Compat;
using Xunit;
using Xunit.Abstractions;

namespace NetForms.Tests;

/// <summary>
/// The second gate of phase 5.0: a <see cref="DefaultValueAttribute"/> that lies is worse than no
/// attribute at all. The designer omits a property from the generated code whenever its value
/// equals the declared default, so a wrong default means either a setting silently lost or a
/// redundant line in every InitializeComponent.
///
/// The sweep instantiates every component we ship and reads back every property that declares a
/// default.
/// </summary>
public class DefaultValueTests
{
    private readonly ITestOutputHelper _output;

    public DefaultValueTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void DeclaredDefaultsMatchAFreshInstance()
    {
        TestPlatform.Install();

        var failures = new List<string>();
        int checkedProperties = 0;

        foreach (var type in Components())
        {
            object instance;
            try { instance = Activator.CreateInstance(type)!; }
            catch (Exception ex)
            {
                _output.WriteLine($"{type.Name}: not constructible ({ex.GetBaseException().GetType().Name}); skipped");
                continue;
            }

            using (instance as IDisposable)
            {
                foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(instance))
                {
                    if (pd.Attributes[typeof(DefaultValueAttribute)] is not DefaultValueAttribute declared) continue;

                    object? actual;
                    try { actual = pd.GetValue(instance); }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"{type.Name}.{pd.Name}: getter threw {ex.GetBaseException().GetType().Name}; skipped");
                        continue;
                    }

                    checkedProperties++;
                    var expectedText = AttributeDump.Format(declared.Value);
                    var actualText = AttributeDump.Format(actual);
                    if (expectedText == actualText) continue;

                    var line = $"{type.Name}.{pd.Name}: [DefaultValue({expectedText ?? "null"})] but a new instance says {actualText ?? "null"}";
                    if (Known.Contains($"{type.Name}.{pd.Name}")) _output.WriteLine("known: " + line);
                    else failures.Add(line);
                }
            }
        }

        _output.WriteLine($"checked {checkedProperties} declared defaults");
        foreach (var f in failures) _output.WriteLine(f);

        Assert.True(failures.Count == 0,
            $"{failures.Count} declared defaults do not match a fresh instance:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// Members whose declared default does not describe a fresh instance, on purpose.
    ///
    /// Two kinds. The first is where the REAL WinForms is inconsistent in exactly the same way, so
    /// matching it means keeping the inconsistency - verified against the reference dump
    /// (tests/NetForms.Compat --attrs), not assumed. The second is a documented difference of ours,
    /// listed under the open questions of phase 5 in docs/PLAN.md; the entry must disappear from
    /// here when the difference is closed.
    /// </summary>
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        // --- WinForms is inconsistent here too --------------------------------------------
        "GroupBox.TabStop", "PictureBox.TabStop",        // [DefaultValue(true)] inherited, actually false
        "DataGridViewTextBoxEditingControl.TabStop", "DataGridViewComboBoxEditingControl.TabStop", // the same: false in the constructor
        "ImageList.Images",                              // [DefaultValue(null)] on a collection
        "TreeView.LineColor",                            // [DefaultValue(Black)], actually Color.Empty
        "ToolStripButton.Name", "ToolStripComboBox.Name", "ToolStripDropDownButton.Name",
        "ToolStripLabel.Name", "ToolStripMenuItem.Name", "ToolStripProgressBar.Name",
        "ToolStripSeparator.Name", "ToolStripSplitButton.Name", "ToolStripStatusLabel.Name",
        "ToolStripTextBox.Name",                         // [DefaultValue(null)], actually ""
        "PrintDialog.PrinterSettings",                   // [DefaultValue(null)], the getter creates one

        // --- ours, tracked in docs/PLAN.md ------------------------------------------------
        // (none: ToolStrip.AutoSize and the drop-downs' Dock were closed in phase 5.2, decision 70)
    };

    /// <summary>Every component we ship that a designer could drop on a form.</summary>
    private static IEnumerable<Type> Components()
        => typeof(Control).Assembly.GetTypes()
            .Where(t => t.IsPublic && !t.IsAbstract && !t.IsGenericTypeDefinition
                        && typeof(IComponent).IsAssignableFrom(t)
                        && t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) != null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);
}
