// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms;

/// <summary>The English texts of the WinForms resources (src/System.Windows.Forms/Resources/SR.resx) the vendored data binding code uses.</summary>
internal static class SR
{
    internal const string BadDataSourceForComplexBinding = "Complex DataBinding accepts as a data source either an IList or an IListSource.";
    internal const string BindingNotSupported = "Binding operations are not supported when application trimming is enabled.";
    internal const string BindingSourceAllowNewDescr = "Determines whether the BindingSource allows new items to be added to the list.";
    internal const string BindingSourceBadSortString = "Sort string not valid.";
    internal const string BindingSourceBindingListWrapperAddToReadOnlyList = "Item cannot be added to a read-only or fixed-size list.";
    internal const string BindingSourceBindingListWrapperNeedAParameterlessConstructor = "AddNew cannot be called on the '{0}' type. This type does not have a public default constructor. You can call AddNew on the '{0}' type if you handle the AddingNew event and create the appropriate object.";
    internal const string BindingSourceBindingListWrapperNeedToSetAllowNew = "AddNew cannot be called on the '{0}' type. This type does not have a public default constructor. You can call AddNew on the '{0}' type if you set AllowNew=true and handle the AddingNew event.";
    internal const string BindingSourceInstanceError = "BindingSource unable to create list based on the Type specified in the DataSource property.";
    internal const string BindingSourceItemTypeIsValueType = "Cannot add null to BindingSource if the underlying list stores value types.";
    internal const string BindingSourceItemTypeMismatchOnAdd = "Objects added to a BindingSource's list must all be of the same type.";
    internal const string BindingSourceRecursionDetected = "BindingSource cannot be its own data source. Do not set the DataSource and DataMember properties to values that refer back to BindingSource.";
    internal const string BindingSourceRemoveCurrentNoCurrentItem = "Current item cannot be removed from the list because there is no current item.";
    internal const string BindingSourceRemoveCurrentNotAllowed = "Current item cannot be removed from the list because the list does not allow removal of items.";
    internal const string BindingSourceSortStringPropertyNotInIBindingList = "Sort string contains a property that is not in the IBindingList.";
    internal const string BindingsCollectionAdd1 = "dataBinding already belongs to this BindingsCollection.";
    internal const string BindingsCollectionAdd2 = "dataBinding belongs to another BindingsCollection.";
    internal const string BindingsCollectionDup = "This causes two bindings in the collection to bind to the same property.";
    internal const string BindingsCollectionForeign = "Binding does not belong to this BindingsCollection.";
    internal const string ComboBoxDataSourceWithSort = "DataSource cannot be set in a combo box that is sorted.";
    internal const string CurrencyManagerCantAddNew = "The list must be an IBindingList to AddNew.";
    internal const string DataBindingAddNewNotSupportedOnPropertyManager = "AddNew is not supported for property to property binding.";
    internal const string DataBindingCycle = "Detected a property binding cycle for the property '{0}'";
    internal const string DataBindingPushDataException = "DataBinding cannot find a row in the list that is suitable for all bindings.";
    internal const string DataBindingRemoveAtNotSupportedOnPropertyManager = "RemoveAt is not supported for property-to-property binding.";
    internal const string DataSourceDataMemberPropNotFound = "DataMember property '{0}' cannot be found on the DataSource.";
    internal const string DataSourceLocksItems = "Items collection cannot be modified when the DataSource property is set.";
    internal const string Formatter_CantConvert = "Value '{0}' cannot be converted to type '{1}'.";
    internal const string Formatter_CantConvertNull = "Null value cannot be converted to type '{1}'.";
    internal const string ListBindingBindField = "Cannot bind to the property or column {0} on the DataSource.";
    internal const string ListBindingBindProperty = "Cannot bind to the property '{0}' on the target control.";
    internal const string ListBindingBindPropertyReadOnly = "Cannot bind to property '{0}' because it is read-only.";
    internal const string ListBindingFormatFailed = "Cannot format the value to the desired type.";
    internal const string ListControlEmptyValueMemberInSettingSelectedValue = "Cannot set the SelectedValue in a ListControl with an empty ValueMember.";
    internal const string ListControlWrongDisplayMember = "Cannot bind to the new display member.";
    internal const string ListControlWrongValueMember = "Cannot bind to the new value member.";
    internal const string ListManagerBadPosition = "Position is either less than 0 or greater than the number of items in the data source.";
    internal const string ListManagerEmptyList = "Cannot operate with an empty list.";
    internal const string ListManagerNoValue = "Index {0} does not have a value.";
    internal const string ListManagerSetDataSource = "Data sources of type {0} are not supported.";
    internal const string NoAllowNewOnReadOnlyList = "AllowNew can only be set to true on an IBindingList or on a read-write list with a default public constructor.";
    internal const string OperationRequiresIBindingList = "This operation requires an IBindingList.";
    internal const string OperationRequiresIBindingListView = "This operation requires an IBindingListView.";
    internal const string PropertyManagerPropDoesNotExist = "Property {0} does not exist in {1}.";
    internal const string PropertyValueInvalidEntry = "One or more entries are not valid in the IDictionary parameter. Verify that all values match up to the object's properties.";
    internal const string RelatedListManagerChild = "Child list for field {0} cannot be created.";
}
