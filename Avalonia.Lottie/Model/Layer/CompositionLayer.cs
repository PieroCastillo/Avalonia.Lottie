using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Lottie.Animation.Content;
using Avalonia.Lottie.Animation.Keyframe;
using Avalonia.Lottie.Value;

namespace Avalonia.Lottie.Model.Layer
{
    internal class CompositionLayer : BaseLayer
    {
        private IBaseKeyframeAnimation<float?, float?> _timeRemapping;
        private readonly List<BaseLayer> _layers = new();
        private Rect _newClipRect;

        private bool? _hasMatte;
        private bool? _hasMasks;

        internal CompositionLayer(LottieDrawable lottieDrawable, Layer layerModel, List<Layer> layerModels, LottieComposition composition) : base(lottieDrawable, layerModel)
        {
            var timeRemapping = layerModel.TimeRemapping;
            if (timeRemapping != null)
            {
                _timeRemapping = timeRemapping.CreateAnimation();
                AddAnimation(_timeRemapping);
                _timeRemapping.ValueChanged += OnValueChanged;
            }
            else
            {
                _timeRemapping = null;
            }

            var layerMap = new Dictionary<long, BaseLayer>(composition.Layers.Count);

            BaseLayer mattedLayer = null;
            for (var i = layerModels.Count - 1; i >= 0; i--)
            {
                var lm = layerModels[i];
                var layer = ForModel(lm, lottieDrawable, composition);
                if (layer == null)
                {
                    continue;
                }
                layerMap[layer.LayerModel.Id] = layer;
                if (mattedLayer != null)
                {
                    mattedLayer.MatteLayer = layer;
                    mattedLayer = null;
                }
                else
                {
                    _layers.Insert(0, layer);
                    switch (lm.GetMatteType())
                    {
                        case Layer.MatteType.Add:
                        case Layer.MatteType.Invert:
                            mattedLayer = layer;
                            break;
                    }
                }
            }

            foreach (var layer in layerMap)
            {
                var layerView = layer.Value;
                // This shouldn't happen but it appears as if sometimes on pre-lollipop devices when 
                // compiled with d8, layerView is null sometimes. 
                // https://github.com/airbnb/lottie-android/issues/524 
                if (layerView == null)
                {
                    continue;
                }
                if (layerMap.TryGetValue(layerView.LayerModel.ParentId, out var parentLayer))
                {
                    layerView.ParentLayer = parentLayer;
                }
            }
        }

        public override void DrawLayer(BitmapCanvas canvas, Matrix3X3 parentMatrix, byte parentAlpha)
        {
            LottieLog.BeginSection("CompositionLayer.Draw");
            canvas.Save();
            RectExt.Set(ref _newClipRect, 0, 0, LayerModel.PreCompWidth, LayerModel.PreCompHeight);
            parentMatrix.MapRect(ref _newClipRect);

            for (var i = _layers.Count - 1; i >= 0; i--)
            {
                var nonEmptyClip = true;
                if (!_newClipRect.IsEmpty)
                {
                    nonEmptyClip = canvas.ClipRect(_newClipRect);
                }
                if (nonEmptyClip)
                {
                    var layer = _layers[i];
                    layer.Draw(canvas, parentMatrix, parentAlpha);
                }
            }
            canvas.Restore();
            LottieLog.EndSection("CompositionLayer.Draw");
        }

        public override void GetBounds(ref Rect outBounds, Matrix3X3 parentMatrix)
        {
            base.GetBounds(ref outBounds, parentMatrix);
            RectExt.Set(ref Rect, 0, 0, 0, 0);
            for (var i = _layers.Count - 1; i >= 0; i--)
            {
                var content = _layers[i];
                content.GetBounds(ref Rect, BoundsMatrix);
                if (outBounds.IsEmpty)
                {
                    RectExt.Set(ref outBounds, Rect);
                }
                else
                {
                    RectExt.Set(ref outBounds, (float)Math.Min(outBounds.Left, Rect.Left), (float)Math.Min(outBounds.Top, Rect.Top), (float)Math.Max(outBounds.Right, Rect.Right), (float)Math.Max(outBounds.Bottom, Rect.Bottom));
                }
            }
        }

        public override float Progress
        {
            set
            {
                base.Progress = value;

                if (_timeRemapping?.Value != null)
                {
                    var duration = LottieDrawable.Composition.Duration;
                    var remappedTime = (long)(_timeRemapping.Value.Value * 1000);
                    value = remappedTime / duration;
                }
                if (LayerModel.TimeStretch != 0)
                {
                    value /= LayerModel.TimeStretch;
                }

                value -= LayerModel.StartProgress;
                for (var i = _layers.Count - 1; i >= 0; i--)
                {
                    _layers[i].Progress = value;
                }
            }
        }

        internal virtual bool HasMasks()
        {
            if (_hasMasks == null)
            {
                for (var i = _layers.Count - 1; i >= 0; i--)
                {
                    var layer = _layers[i];
                    if (layer is ShapeLayer)
                    {
                        if (layer.HasMasksOnThisLayer())
                        {
                            _hasMasks = true;
                            return true;
                        }
                    }
                    else if (layer is CompositionLayer compositionLayer && compositionLayer.HasMasks())
                    {
                        _hasMasks = true;
                        return true;
                    }
                }
                _hasMasks = false;
            }
            return _hasMasks.Value;
        }

        internal virtual bool HasMatte()
        {
            if (_hasMatte == null)
            {
                if (HasMatteOnThisLayer())
                {
                    _hasMatte = true;
                    return true;
                }

                for (var i = _layers.Count - 1; i >= 0; i--)
                {
                    if (_layers[i].HasMatteOnThisLayer())
                    {
                        _hasMatte = true;
                        return true;
                    }
                }
                _hasMatte = false;
            }
            return _hasMatte.Value;
        }

        internal override void ResolveChildKeyPath(KeyPath keyPath, int depth, List<KeyPath> accumulator, KeyPath currentPartialKeyPath)
        {
            for (var i = 0; i < _layers.Count; i++)
            {
                _layers[i].ResolveKeyPath(keyPath, depth, accumulator, currentPartialKeyPath);
            }
        }

        public override void AddValueCallback<T>(LottieProperty property, ILottieValueCallback<T> callback)
        {
            base.AddValueCallback(property, callback);

            if (property == LottieProperty.TimeRemap)
            {
                if (callback == null)
                {
                    _timeRemapping = null;
                }
                else
                {
                    _timeRemapping = new ValueCallbackKeyframeAnimation<float?, float?>((ILottieValueCallback<float?>)callback);
                    AddAnimation(_timeRemapping);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                foreach (var item in _layers)
                    item.Dispose();

                _layers.Clear();
            }
        }
    }
}