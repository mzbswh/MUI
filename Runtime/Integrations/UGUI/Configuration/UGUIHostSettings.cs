using System;
using System.Collections.Generic;
using MUI.Pages;
using MUI.Unity;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace MUI.UGUI
{
    [MovedFrom(true, "MUI.UGUI", "MUI.UGUI", "MUISettings")]
    [CreateAssetMenu(menuName = "MUI/uGUI Host Settings", fileName = "MUISettings")]
    public sealed class UGUIHostSettings : MUISettings
    {
        [SerializeField] private string renderSortingLayer = "Default";
        [SerializeField] private RectTransform defaultModalBackdropPrefab = null;
        [SerializeField] private Color defaultModalBackdropColor = new Color(0, 0, 0, 0.45f);
        [SerializeField] private bool animationsEnabled = true;
        [SerializeField] private ViewTransitionAsset defaultEnterTransition;
        [SerializeField] private ViewTransitionAsset defaultExitTransition;
        [SerializeField] private bool automaticFramePump = true;
        public RectTransform DefaultModalBackdropPrefab => defaultModalBackdropPrefab;
        public Color DefaultModalBackdropColor => defaultModalBackdropColor;
        public bool AnimationsEnabled { get => animationsEnabled; set => animationsEnabled = value; }
        public ViewTransitionAsset DefaultEnterTransition { get => defaultEnterTransition; set => defaultEnterTransition = value; }
        public ViewTransitionAsset DefaultExitTransition { get => defaultExitTransition; set => defaultExitTransition = value; }
        public bool AutomaticFramePump => automaticFramePump;

        public override RenderOrderOptions CreateRenderOrderOptions()
        {
            var configuration = CreateConfiguration();
            var options = configuration.CreateRenderOrderOptions();
            foreach (var layer in SortingLayer.layers)
                if (layer.name == renderSortingLayer)
                    return new RenderOrderOptions(options.Minimum, options.Maximum, configuration.DefaultPageOrderSpan,
                        configuration.PreferredOrderGap, layer.id, CreateLayerGaps(), configuration.PreferredLayerGap);
            throw new InvalidOperationException("不存在渲染 Sorting Layer：" + renderSortingLayer);
        }

        private Dictionary<int, int> CreateLayerGaps()
        {
            var gaps = new Dictionary<int, int>();
            foreach (var layer in Layers)
                if (layer != null && layer.LayerGapAfter >= 0) gaps.Add(layer.Order, layer.LayerGapAfter);
            return gaps;
        }

        public override IReadOnlyList<string> Validate()
        {
            var errors = new List<string>(base.Validate());
            try { ModalBackdropStyle.ValidateVisualPrefab(defaultModalBackdropPrefab); }
            catch (InvalidOperationException error) { errors.Add(error.Message); }
            try { CreateRenderOrderOptions(); }
            catch (Exception error) { errors.Add(error.Message); }
            return errors.AsReadOnly();
        }
    }
}
