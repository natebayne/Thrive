using System;
using System.Collections.Generic;
using Godot;

public class FilterWindow : CustomConfirmationDialog
{
    // Paths for nodes
    [Export]
    public NodePath FiltersContainerPath = null!;

    private readonly List<Filter> filters = new();

    private PackedScene multipleChoiceFilterArgumentScene =
        GD.Load<PackedScene>("res://src/gui_common/dialogs/MultipleChoiceFilterArgumentButton.tscn");
    private PackedScene filterArgumentSliderScene =
        GD.Load<PackedScene>("res://src/gui_common/dialogs/FilterArgumentSlider.tscn");

    /// <summary>
    ///   If redraw is needed.
    /// </summary>
    private bool dirty;

    // Nodes
    private VBoxContainer filtersContainer = null!;

    public override void _Ready()
    {
        base._Ready();

        filtersContainer = GetNode<VBoxContainer>(FiltersContainerPath);
    }

    public override void _Process(float delta)
    {
        base._Process(delta);

        if (dirty)
        {
            RectSize = GetCombinedMinimumSize();
        }

        dirty = false;
    }

    public void Initialize(Filter filter, string defaultText = "--")
    {
        ClearFilters();
        SetupFilter(filter, defaultText);
    }

    public void ClearFilters()
    {
        filters.Clear();
        filtersContainer.FreeChildren(true);
    }

    public void SetupFilter(Filter filter, string defaultText = "--")
    {
        if (filtersContainer == null)
            throw new SceneTreeAttachRequired();

        var filterContainer = new HBoxContainer();

        var filterButton = new CustomDropDown();
        filterButton.Text = defaultText;

        foreach (var option in filter.FilterItemsNames)
        {
            filterButton.AddItem(option, false, Colors.White);
        }

        filterButton.CreateElements();
        filterButton.Popup.Connect("index_pressed", this, nameof(OnNewFilterCategorySelected),
            new Godot.Collections.Array { filters.Count });

        filterContainer.AddChild(filterButton);
        filtersContainer.AddChild(filterContainer);
        filters.Add(filter);

        dirty = true;
    }

    private void OnNewFilterCategorySelected(int categoryIndex, int filterIndex)
    {
        var filterCategoryButton = GetFilterControl<CustomDropDown>(0, filterIndex);

        var filterCategory = filterCategoryButton.Popup.GetItemText(categoryIndex);
        filterCategoryButton.Text = filterCategory;

        filters[0].FilterCategory = filterCategory;

        UpdateFilterArguments(filterIndex, filterCategory);

        dirty = true;
    }

    /*private void OnNewFilterArgumentSelected(int argumentValueIndex, int argumentIndex, int filterIndex)
    {
        // Update GUI
        var filterArgumentButton = GetFilterControl<CustomDropDown>(1 + argumentIndex, filterIndex);

        var argumentValue = filterArgumentButton.Popup.GetItemText(argumentValueIndex);
        filterArgumentButton.Text = argumentValue;

        // Update stored values
        // TODO Exceptions
        filters[filterIndex].SetArgumentValue(argumentIndex, argumentValue);

        dirty = true;
    }*/

    /*private void OnNewSliderValueSelected(float argumentValue, int argumentIndex, int filterIndex)
    {
        // Update GUI
        var filterArgumentContainer = GetFilterControl(1 + argumentIndex, filterIndex);

        var filterArgumentSlider = filterArgumentContainer.GetChild<HSlider>(0);
        var filterArgumentLabel = filterArgumentContainer.GetChild<Label>(1);

        if (filterArgumentSlider == null)
        {
            throw new SceneTreeAttachRequired($"Number argument with index {argumentIndex} has no children" +
                $" (expected HSlider)!");
        }

        if (filterArgumentLabel == null)
        {
            throw new SceneTreeAttachRequired($"Number argument with index {argumentIndex} has only one child" +
                $" (expected Label)!");
        }

        filterArgumentLabel.Text = argumentValue.ToString(CultureInfo.CurrentCulture);

        // Update stored values
        // TODO Exceptions
        filters[filterIndex].SetArgumentValue(argumentIndex, argumentValue);

        dirty = true;
    }*/

    /// <summary>
    ///   Returns the control node or group of a filter at given indices and cast it to the desired <see cref="Node"/> subtype.
    /// </summary>
    private T GetFilterControl<T>(int controlIndex, int filterIndex)
        where T : Node
    {
        var filterContainer = filtersContainer.GetChild(filterIndex);

        if (filterContainer == null)
            throw new ArgumentOutOfRangeException($"Filter node {filterIndex} doesn't exist!");

        // First child is filter category;
        var filterArgument = filterContainer.GetChild<T>(controlIndex);

        if (filterArgument == null)
            throw new SceneTreeAttachRequired($"Filter has no node at index {controlIndex}!");

        return filterArgument;
    }

    /// <summary>
    ///   Returns the argument node of a filter at given indices.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Default implementation of <see cref="GetFilterControl{T}(int, int)"/> with <see cref="Node"/> type.
    ///   </para>
    /// </remarks>
    private Node GetFilterControl(int argumentIndex, int filterIndex)
    {
        return GetFilterControl<Node>(argumentIndex, filterIndex);
    }

    private void UpdateFilterArguments(int filterIndex, string filterCategory)
    {
        var filter = filters[filterIndex];

        if (filter == null)
            throw new ArgumentOutOfRangeException($"No filter registered at index {filterIndex}");

        var filterNode = filtersContainer.GetChild(filterIndex);

        if (filterNode == null)
            throw new SceneTreeAttachRequired($"No filter node registered at index {filterIndex}");

        ClearFilterArguments(filterNode);

        if (!filter.FilterItems.TryGetValue(filterCategory, out var filterItem))
            throw new KeyNotFoundException($"Invalid filter category: {filterCategory}");

        for (var i = 0; i < filterItem.FilterArguments.Count; i++)
        {
            // We do not use foreach to keep count of i;
            var filterArgument = filterItem.FilterArguments[i];

            if (filterArgument is Filter.MultipleChoiceFilterArgument multipleChoiceFilterArgument)
            {
                // TODO use name FilterArgumentButton for class?
                var filterArgumentButton = (MultipleChoiceFilterArgumentButton)multipleChoiceFilterArgumentScene
                    .Instance();
                filterArgumentButton.Initialize(multipleChoiceFilterArgument);
                filterNode.AddChild(filterArgumentButton);
            }
            else if (filterArgument is Filter.NumberFilterArgument numberFilterArgument)
            {
                var filterArgumentSlider = (FilterArgumentSlider)filterArgumentSliderScene.Instance();
                filterArgumentSlider.Initialize(numberFilterArgument);
                filterNode.AddChild(filterArgumentSlider);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        dirty = true;
    }

    /// <summary>
    ///   Removes all arguments from a filter, but keeps the category.
    /// </summary>
    private void ClearFilterArguments(Node filterNode)
    {
        for (var i = 1; i < filterNode.GetChildCount(); i++)
        {
            var nodeToRemove = filterNode.GetChild(i);
            filterNode.RemoveChild(nodeToRemove);

            // We free for memory, but keeping could allow to save options...
            nodeToRemove.Free();
        }

        dirty = true;
    }
}
