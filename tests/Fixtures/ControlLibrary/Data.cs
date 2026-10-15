using System.Collections.Generic;

namespace ControlLibrary;

/// <summary>A class of the project to bind to (an object data source in VS): DesignerDataBindingTests.</summary>
public class Customer
{
    public string Name { get; set; } = "";

    public int Age { get; set; }

    public bool Active { get; set; }

    public List<Order> Orders { get; } = new();
}

public class Order
{
    public string Product { get; set; } = "";

    public decimal Price { get; set; }
}
