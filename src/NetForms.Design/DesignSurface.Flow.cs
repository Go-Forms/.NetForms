using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace NetForms.Design;

/// <summary>
/// FlowLayoutPanel on the design surface, as VS's FlowLayoutPanelDesigner: a control in a flow is placed by its
/// order in <c>Controls</c> (the first one is the first in the flow), not by coordinates. A drop goes where the
/// insertion mark was; a click in the toolbox, a paste, appends - before, a new control was put first in Controls,
/// so it came first in the flow, and a drag inside the panel did nothing (the layout put the control back).
/// </summary>
public sealed partial class DesignSurface
{
    /// <summary>A control put into a flow: at <paramref name="index"/> (its place in the flow), or at the end.</summary>
    private static void PlaceInFlow(FlowLayoutPanel flow, Control control, int? index)
    {
        flow.Controls.Add(control);
        if (index is { } at) flow.Controls.SetChildIndex(control, Math.Clamp(at, 0, flow.Controls.Count - 1));
    }

    /// <summary>
    /// A control of a flow moved to the insertion point <paramref name="index"/>, counted with the control still in
    /// its place (0 = before the first; Count = after the last).
    /// </summary>
    private static void MoveInFlow(Control control, int index)
    {
        var controls = control.Parent!.Controls;
        int old = controls.GetChildIndex(control);
        int target = Math.Clamp(index > old ? index - 1 : index, 0, controls.Count - 1);
        if (target != old) controls.SetChildIndex(control, target);
    }

    /// <summary>The controls of a container in the order given; those not given follow in the order they had.</summary>
    private void Reorder(Control container, IReadOnlyList<string?> ids)
    {
        var order = new List<Control>();
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id)) continue;
            var control = AsControl(Resolve(id));
            if (!ReferenceEquals(control.Parent, container)) throw new DesignerEditException($"'{id}' is not in {Name(container)}.");
            if (order.Contains(control)) throw new DesignerEditException($"'{id}' is listed twice.");
            order.Add(control);
        }
        order.AddRange(container.Controls.Cast<Control>().Where(c => !order.Contains(c)).ToList());
        for (int i = 0; i < order.Count; i++)
            if (container.Controls.GetChildIndex(order[i]) != i) container.Controls.SetChildIndex(order[i], i);
    }
}
