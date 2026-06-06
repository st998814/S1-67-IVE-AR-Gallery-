using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Bottom-center scene panel for semantic Move / Scale sliders (content inspector only).
/// </summary>
public sealed class AuthoringUIManipulatorPanel
{
    private VisualElement _rootPanel;
    private VisualElement _moveControlsGroup;
    private VisualElement _scaleControlsGroup;
    private Slider _moveLeftRightSlider;
    private Slider _moveUpDownSlider;
    private Slider _moveCloserFurtherSlider;
    private Slider _uniformScaleSlider;
    private Label _moveLeftRightRowLabel;
    private Label _moveUpDownRowLabel;
    private Label _moveCloserFurtherRowLabel;
    private Label _moveLeftRightValueLabel;
    private Label _moveUpDownValueLabel;
    private Label _moveCloserFurtherValueLabel;
    private Label _uniformScaleValueLabel;

    private ContentTransformManipulator _manipulator;
    private PlacementBoundsService _placementBounds;
    private TargetLocalTransformService _localTransformService;
    private Func<Transform> _getSelectedContent;
    private Func<bool> _isContentInspectorActive;
    private Func<TransformGizmoController.GizmoMode> _getGizmoMode;
    private Func<SemanticAxisMapping.PlacementPosture> _getPlacementPosture;
    private Action _onTransformEdited;

    private bool _suppressSliderCallbacks;

    public void Bind(
        VisualElement root,
        ContentTransformManipulator manipulator,
        PlacementBoundsService placementBounds,
        TargetLocalTransformService localTransformService,
        Func<Transform> getSelectedContent,
        Func<bool> isContentInspectorActive,
        Func<TransformGizmoController.GizmoMode> getGizmoMode,
        Func<SemanticAxisMapping.PlacementPosture> getPlacementPosture,
        Action onTransformEdited)
    {
        _manipulator = manipulator;
        _placementBounds = placementBounds;
        _localTransformService = localTransformService;
        _getSelectedContent = getSelectedContent;
        _isContentInspectorActive = isContentInspectorActive;
        _getGizmoMode = getGizmoMode;
        _getPlacementPosture = getPlacementPosture;
        _onTransformEdited = onTransformEdited;

        _rootPanel = root.Q<VisualElement>("ManipulatorBottomPanel");
        _moveControlsGroup = root.Q<VisualElement>("MoveControlsGroup");
        _scaleControlsGroup = root.Q<VisualElement>("ScaleControlsGroup");
        _moveLeftRightSlider = root.Q<Slider>("MoveLeftRightSlider");
        _moveUpDownSlider = root.Q<Slider>("MoveUpDownSlider");
        _moveCloserFurtherSlider = root.Q<Slider>("MoveCloserFurtherSlider");
        _uniformScaleSlider = root.Q<Slider>("UniformScaleSlider");
        _moveLeftRightRowLabel = root.Q<Label>("MoveLeftRightRowLabel");
        _moveUpDownRowLabel = root.Q<Label>("MoveUpDownRowLabel");
        _moveCloserFurtherRowLabel = root.Q<Label>("MoveCloserFurtherRowLabel");
        _moveLeftRightValueLabel = root.Q<Label>("MoveLeftRightValueLabel");
        _moveUpDownValueLabel = root.Q<Label>("MoveUpDownValueLabel");
        _moveCloserFurtherValueLabel = root.Q<Label>("MoveCloserFurtherValueLabel");
        _uniformScaleValueLabel = root.Q<Label>("UniformScaleValueLabel");

        RegisterSliderCallbacks();
        RefreshVisibilityAndValues();
    }

    public void RegisterModePill(VisualElement pill, TransformGizmoController.GizmoMode mode, Action<TransformGizmoController.GizmoMode> setMode)
    {
        if (pill == null || setMode == null)
            return;

        pill.RegisterCallback<ClickEvent>(_ => setMode(mode));
    }

    public void RefreshVisibilityAndValues()
    {
        if (_rootPanel == null)
            return;

        bool contentActive = _isContentInspectorActive != null && _isContentInspectorActive();
        Transform content = _getSelectedContent != null ? _getSelectedContent() : null;
        TransformGizmoController.GizmoMode mode = _getGizmoMode != null
            ? _getGizmoMode()
            : TransformGizmoController.GizmoMode.Translate;

        bool showMove = contentActive && content != null && mode == TransformGizmoController.GizmoMode.Translate;
        bool showScale = contentActive && content != null && mode == TransformGizmoController.GizmoMode.Scale;
        bool showPanel = showMove || showScale;

        _rootPanel.EnableInClassList("manipulator-bottom-panel--hidden", !showPanel);
        _rootPanel.style.display = showPanel ? DisplayStyle.Flex : DisplayStyle.None;

        if (_moveControlsGroup != null)
            _moveControlsGroup.style.display = showMove ? DisplayStyle.Flex : DisplayStyle.None;
        if (_scaleControlsGroup != null)
            _scaleControlsGroup.style.display = showScale ? DisplayStyle.Flex : DisplayStyle.None;

        if (!showPanel || content == null)
            return;

        if (showMove)
            SyncMoveSlidersFromContent(content);
        if (showScale)
            SyncScaleSliderFromContent(content);
    }

    public bool IsManipulatorSliderFocused(Focusable focused)
    {
        if (focused == null)
            return false;

        return focused == _moveLeftRightSlider
            || focused == _moveUpDownSlider
            || focused == _moveCloserFurtherSlider
            || focused == _uniformScaleSlider;
    }

    private void RegisterSliderCallbacks()
    {
        if (_moveLeftRightSlider != null)
            _moveLeftRightSlider.RegisterValueChangedCallback(evt => OnSemanticMoveSliderChanged(
                PlacementBoundsCalculator.SemanticAxis.LeftRight, evt.newValue, _moveLeftRightValueLabel));

        if (_moveUpDownSlider != null)
            _moveUpDownSlider.RegisterValueChangedCallback(evt => OnSemanticMoveSliderChanged(
                PlacementBoundsCalculator.SemanticAxis.UpDown, evt.newValue, _moveUpDownValueLabel));

        if (_moveCloserFurtherSlider != null)
            _moveCloserFurtherSlider.RegisterValueChangedCallback(evt => OnSemanticMoveSliderChanged(
                PlacementBoundsCalculator.SemanticAxis.CloserFurther, evt.newValue, _moveCloserFurtherValueLabel));

        if (_uniformScaleSlider != null)
            _uniformScaleSlider.RegisterValueChangedCallback(OnUniformScaleSliderChanged);
    }

    private void OnSemanticMoveSliderChanged(PlacementBoundsCalculator.SemanticAxis axis, float value, Label valueLabel)
    {
        if (_suppressSliderCallbacks)
            return;

        Transform content = _getSelectedContent != null ? _getSelectedContent() : null;
        if (content == null || _manipulator == null)
            return;

        _manipulator.SetSemanticAxis(content, axis, value);
        UpdateValueLabel(valueLabel, content, axis);
        _onTransformEdited?.Invoke();
    }

    private void OnUniformScaleSliderChanged(ChangeEvent<float> evt)
    {
        if (_suppressSliderCallbacks)
            return;

        Transform content = _getSelectedContent != null ? _getSelectedContent() : null;
        if (content == null || _manipulator == null)
            return;

        _manipulator.SetUniformScale(content, evt.newValue);
        UpdateScaleValueLabel(_uniformScaleValueLabel, content.localScale.x);
        _onTransformEdited?.Invoke();
    }

    private void SyncMoveSlidersFromContent(Transform content)
    {
        if (content == null)
            return;

        SemanticAxisMapping.PlacementPosture posture = ResolvePlacementPosture();
        ApplyRowLabels(posture);

        _suppressSliderCallbacks = true;
        try
        {
            Vector3 lp = content.localPosition;

            if (_placementBounds != null)
            {
                ApplyAxisRange(_moveLeftRightSlider, _placementBounds.GetAxisRange(content, PlacementBoundsCalculator.SemanticAxis.LeftRight));
                ApplyAxisRange(_moveUpDownSlider, _placementBounds.GetAxisRange(content, PlacementBoundsCalculator.SemanticAxis.UpDown));
                ApplyAxisRange(_moveCloserFurtherSlider, _placementBounds.GetAxisRange(content, PlacementBoundsCalculator.SemanticAxis.CloserFurther));
            }

            SetSliderValue(_moveLeftRightSlider, posture, PlacementBoundsCalculator.SemanticAxis.LeftRight, lp);
            SetSliderValue(_moveUpDownSlider, posture, PlacementBoundsCalculator.SemanticAxis.UpDown, lp);
            SetSliderValue(_moveCloserFurtherSlider, posture, PlacementBoundsCalculator.SemanticAxis.CloserFurther, lp);

            UpdateSemanticValueLabel(_moveLeftRightValueLabel, posture, PlacementBoundsCalculator.SemanticAxis.LeftRight, lp);
            UpdateSemanticValueLabel(_moveUpDownValueLabel, posture, PlacementBoundsCalculator.SemanticAxis.UpDown, lp);
            UpdateSemanticValueLabel(_moveCloserFurtherValueLabel, posture, PlacementBoundsCalculator.SemanticAxis.CloserFurther, lp);
        }
        finally
        {
            _suppressSliderCallbacks = false;
        }
    }

    private void SyncScaleSliderFromContent(Transform content)
    {
        if (content == null)
            return;

        _suppressSliderCallbacks = true;
        try
        {
            if (_uniformScaleSlider != null && _localTransformService != null)
            {
                _uniformScaleSlider.lowValue = _localTransformService.MinUniformScale;
                _uniformScaleSlider.highValue = _localTransformService.MaxUniformScale;
                _uniformScaleSlider.value = content.localScale.x;
            }

            UpdateScaleValueLabel(_uniformScaleValueLabel, content.localScale.x);
        }
        finally
        {
            _suppressSliderCallbacks = false;
        }
    }

    private SemanticAxisMapping.PlacementPosture ResolvePlacementPosture()
    {
        if (_getPlacementPosture != null)
            return _getPlacementPosture();

        if (_placementBounds != null)
            return _placementBounds.ActivePosture;

        return SemanticAxisMapping.PlacementPosture.Wall;
    }

    private void ApplyRowLabels(SemanticAxisMapping.PlacementPosture posture)
    {
        SemanticAxisMapping.RowLabels labels = SemanticAxisMapping.GetRowLabels(posture);
        if (_moveLeftRightRowLabel != null)
            _moveLeftRightRowLabel.text = labels.leftRight;
        if (_moveUpDownRowLabel != null)
            _moveUpDownRowLabel.text = labels.middle;
        if (_moveCloserFurtherRowLabel != null)
            _moveCloserFurtherRowLabel.text = labels.standoff;
    }

    private static void SetSliderValue(
        Slider slider,
        SemanticAxisMapping.PlacementPosture posture,
        PlacementBoundsCalculator.SemanticAxis axis,
        Vector3 localPosition)
    {
        if (slider == null)
            return;

        slider.value = SemanticAxisMapping.GetComponentValue(posture, axis, localPosition);
    }

    private static void ApplyAxisRange(Slider slider, PlacementBoundsCalculator.AxisRange range)
    {
        if (slider == null)
            return;

        slider.lowValue = range.min;
        slider.highValue = range.max;
        slider.value = Mathf.Clamp(slider.value, range.min, range.max);
    }

    private void UpdateValueLabel(Label label, Transform content, PlacementBoundsCalculator.SemanticAxis axis)
    {
        if (label == null || content == null)
            return;

        UpdateSemanticValueLabel(label, ResolvePlacementPosture(), axis, content.localPosition);
    }

    private static void UpdateSemanticValueLabel(
        Label label,
        SemanticAxisMapping.PlacementPosture posture,
        PlacementBoundsCalculator.SemanticAxis axis,
        Vector3 localPosition)
    {
        if (label == null)
            return;

        float metres = SemanticAxisMapping.GetComponentValue(posture, axis, localPosition);
        label.text = SemanticDistanceFormatter.FormatOffset(posture, axis, metres);
    }

    private static void UpdateScaleValueLabel(Label label, float scale)
    {
        if (label == null)
            return;

        label.text = SemanticDistanceFormatter.FormatUniformScale(scale);
    }
}
