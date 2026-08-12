using Godot;
using System.Collections.Generic;

public partial class OutroAnimator : Node
{
    [Export]
    public Godot.Collections.Array<Sprite2D> TargetSprites { get; set; } = new Godot.Collections.Array<Sprite2D>();

    [Export]
    public AudioStreamPlayer AudioPlayer { get; set; } = default!;

    [Export]
    public CanvasItem FlashOverlay { get; set; } = default!;

    [Export]
    public Control MusicLabel { get; set; } = default!;

    [Export]
    public Sprite2D VideoBg1 { get; set; } = default!;

    [Export]
    public Sprite2D VideoBg2 { get; set; } = default!;

    [Export]
    public Sprite2D LogoBgCircle { get; set; } = default!;

    [Export]
    public float OffscreenDistance { get; set; } = 1500f;

    [Export]
    public float PeakDbThreshold { get; set; } = -1.1f;

    [Export]
    public float FlashDelay { get; set; } = 3.69f;

    private class TransformData
    {
        public Vector2 Position;
        public float Rotation;
        public Vector2 Scale;
        public Color Modulate;
        public float Skew;
    }

    private class ImpactOffsetData
    {
        public Vector2 Scale { get; set; } = Vector2.One;
        public float Rotation { get; set; } = 0f;
        public Color ModulateMultiplier { get; set; } = new Color(1f, 1f, 1f, 1f);
    }

    private Dictionary<Sprite2D, TransformData> _originalTransforms = new();
    private Dictionary<Sprite2D, TransformData> _baseTransforms = new();
    private Dictionary<Sprite2D, ImpactOffsetData> _impactOffsets = new();
    private Dictionary<Sprite2D, Tween> _activeImpactTweens = new();
    private HashSet<Sprite2D> _visibleSprites = new();

    private Vector2 _labelOriginalPosition;
    private Vector2 _labelBasePosition;
    private Color _labelOriginalModulate;
    private Color _labelBaseModulate;

    private Vector2 _videoBg1OriginalPos;
    private Vector2 _videoBg2OriginalPos;
    private Vector2 _logoBgCircleOriginalPos;

    private Tween? _flashTween;
    private RandomNumberGenerator _rng = new();

    private int _audioBusIndex = 0;
    private bool _hasFlashed = false;
    private bool _isFadingOut = false;

    public override void _Ready()
    {
        _rng.Randomize();

        FlashOverlay.Modulate = Colors.Black;
        if (FlashOverlay.Material is ShaderMaterial mat)
        {
            mat.SetShaderParameter("progress", 1.0f);
        }

        Tween flashDelayTween = CreateTween();

        flashDelayTween.TweenProperty(FlashOverlay.Material, "shader_parameter/progress", 0.0f, FlashDelay);
        flashDelayTween.TweenCallback(Callable.From(TriggerFlash));

        _labelOriginalPosition = MusicLabel.Position;
        _labelOriginalModulate = MusicLabel.Modulate;
        _labelBasePosition = _labelOriginalPosition;
        _labelBaseModulate = _labelOriginalModulate;
        _labelBaseModulate.A = 0;

        _videoBg1OriginalPos = VideoBg1.Position;
        VideoBg1.Position = _videoBg1OriginalPos + new Vector2(OffscreenDistance, 0);

        _videoBg2OriginalPos = VideoBg2.Position;
        VideoBg2.Position = _videoBg2OriginalPos + new Vector2(OffscreenDistance, 0);

        _logoBgCircleOriginalPos = LogoBgCircle.Position;
        LogoBgCircle.Position = _logoBgCircleOriginalPos - new Vector2(0, OffscreenDistance);

        for (int i = 0; i < TargetSprites.Count; i++)
        {
            var sprite = TargetSprites[i];

            _originalTransforms[sprite] = new TransformData
            {
                Position = sprite.Position,
                Rotation = sprite.Rotation,
                Scale = sprite.Scale,
                Modulate = sprite.Modulate,
                Skew = sprite.Skew
            };

            _baseTransforms[sprite] = new TransformData
            {
                Position = sprite.Position,
                Rotation = sprite.Rotation,
                Scale = sprite.Scale,
                Modulate = sprite.Modulate,
                Skew = sprite.Skew
            };

            _impactOffsets[sprite] = new ImpactOffsetData();
        }

        for (int i = 0; i < TargetSprites.Count; i++)
        {
            var sprite = TargetSprites[i];

            float startTime = i * 0.5f;
            float duration = 1.0f;

            AnimateSprite(sprite, startTime, duration);
        }

        _audioBusIndex = AudioServer.GetBusIndex(AudioPlayer.Bus);
        AudioPlayer.Finished += OnAudioFinished;
        AudioPlayer.Play();

        AudioVisualizer visualizer = new AudioVisualizer() { AudioPlayer = AudioPlayer };
        visualizer.ZIndex = 0;
        AddChild(visualizer);
    }

    public override void _Process(double delta)
    {
        if (AudioPlayer.Playing)
        {
            float peakLeft = AudioServer.GetBusPeakVolumeLeftDb(_audioBusIndex, 0);
            float peakRight = AudioServer.GetBusPeakVolumeRightDb(_audioBusIndex, 0);
            float currentDb = Mathf.Max(peakLeft, peakRight);

            if (currentDb >= PeakDbThreshold)
            {
                TriggerImpactOnAllSprites();
            }

            if (!_isFadingOut)
            {
                float streamLength = (float)AudioPlayer.Stream.GetLength();
                float currentPosition = AudioPlayer.GetPlaybackPosition();

                if (streamLength > 0f && (streamLength - currentPosition) <= 1.0f)
                {
                    TriggerFadeOut();
                }
            }
        }

        ApplyCombinedTransforms();
    }

    private void TriggerFadeOut()
    {
        _isFadingOut = true;

        _flashTween?.Kill();

        FlashOverlay.Modulate = Colors.Black;

        _flashTween = CreateTween();
        _flashTween.SetParallel(true);

        _flashTween.TweenProperty(FlashOverlay.Material, "shader_parameter/progress", 1.0f, 1.0f)
                   .SetEase(Tween.EaseType.In)
                   .SetTrans(Tween.TransitionType.Quad);

        _flashTween.TweenProperty(AudioPlayer, "volume_db", -80.0f, 1.0f)
                   .SetEase(Tween.EaseType.In)
                   .SetTrans(Tween.TransitionType.Quad);
    }

    private void AnimateSprite(Sprite2D sprite, float delay, float duration)
    {
        var target = _originalTransforms[sprite];
        var baseTr = _baseTransforms[sprite];

        Vector2 startPos = target.Position;
        int direction = _rng.RandiRange(0, 3);
        switch (direction)
        {
            case 0: startPos.Y -= OffscreenDistance; break;
            case 1: startPos.X += OffscreenDistance; break;
            case 2: startPos.Y += OffscreenDistance; break;
            case 3: startPos.X -= OffscreenDistance; break;
        }

        Color startColor = target.Modulate;
        startColor.A = 0;

        float rotationAmount = Mathf.Pi * 2;
        int rotDirection = _rng.RandiRange(0, 1) == 0 ? 1 : -1;
        float startRotation = target.Rotation + (rotationAmount * rotDirection);

        baseTr.Position = startPos;
        baseTr.Modulate = startColor;
        baseTr.Scale = Vector2.Zero;
        baseTr.Rotation = startRotation;

        Tween tween = CreateTween();
        tween.SetParallel(true);
        tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart);

        tween.TweenMethod(Callable.From<Vector2>(v => baseTr.Position = v), startPos, target.Position, duration).SetDelay(delay);
        tween.TweenMethod(Callable.From<Color>(c => baseTr.Modulate = c), startColor, target.Modulate, duration).SetDelay(delay);
        tween.TweenMethod(Callable.From<Vector2>(s => baseTr.Scale = s), Vector2.Zero, target.Scale, duration).SetDelay(delay);
        tween.TweenMethod(Callable.From<float>(r => baseTr.Rotation = r), startRotation, target.Rotation, duration).SetDelay(delay);

        tween.Chain().TweenCallback(Callable.From(() =>
        {
            _visibleSprites.Add(sprite);
        }));
    }

    private void AnimateLabelPostFlash()
    {
        Vector2 startPos = _labelOriginalPosition;
        int direction = _rng.RandiRange(0, 1);
        if (direction == 0)
        {
            startPos.Y += OffscreenDistance;
        }
        else
        {
            startPos.X -= OffscreenDistance;
        }

        _labelBasePosition = startPos;
        _labelBaseModulate = _labelOriginalModulate;

        Tween tween = CreateTween();

        tween.TweenMethod(Callable.From<Vector2>(v => _labelBasePosition = v), startPos, _labelOriginalPosition, 5.0f)
             .SetEase(Tween.EaseType.Out)
             .SetTrans(Tween.TransitionType.Quart);

        tween.Chain().TweenMethod(Callable.From<Vector2>(v => _labelBasePosition = v), _labelOriginalPosition, startPos, 2.0f)
             .SetEase(Tween.EaseType.In)
             .SetTrans(Tween.TransitionType.Quart);
    }

    private void TriggerImpactOnAllSprites()
    {
        foreach (var sprite in TargetSprites)
        {
            ApplyImpact(sprite);
        }
    }

    private void ApplyImpact(Sprite2D sprite)
    {
        if (_activeImpactTweens.TryGetValue(sprite, out var existingTween) && existingTween.IsValid())
        {
            existingTween.Kill();
        }

        var offset = _impactOffsets[sprite];

        float randomRotOffset = _rng.RandfRange(-0.15f, 0.15f);
        float stretchX = _rng.RandfRange(0.85f, 1.25f);
        float stretchY = 2.1f - stretchX;

        Vector2 targetImpactScale = new Vector2(stretchX, stretchY);

        Tween tween = CreateTween();
        _activeImpactTweens[sprite] = tween;

        float hitDuration = 0.12f;
        float recoverDuration = 0.4f;

        tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenMethod(Callable.From<Vector2>(s => offset.Scale = s), offset.Scale, targetImpactScale, hitDuration);
        tween.Parallel().TweenMethod(Callable.From<float>(r => offset.Rotation = r), offset.Rotation, randomRotOffset, hitDuration);

        tween.Chain();

        tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
        tween.TweenMethod(Callable.From<Vector2>(s => offset.Scale = s), targetImpactScale, Vector2.One, recoverDuration);
        tween.Parallel().TweenMethod(Callable.From<float>(r => offset.Rotation = r), randomRotOffset, 0f, recoverDuration);
    }

    private void ApplyCombinedTransforms()
    {
        foreach (var sprite in TargetSprites)
        {
            var baseTr = _baseTransforms[sprite];
            var offset = _impactOffsets[sprite];

            sprite.Position = baseTr.Position;
            sprite.Rotation = baseTr.Rotation + offset.Rotation;
            sprite.Scale = baseTr.Scale * offset.Scale;
            sprite.Skew = baseTr.Skew;
            sprite.Modulate = new Color(
                baseTr.Modulate.R * offset.ModulateMultiplier.R,
                baseTr.Modulate.G * offset.ModulateMultiplier.G,
                baseTr.Modulate.B * offset.ModulateMultiplier.B,
                baseTr.Modulate.A * offset.ModulateMultiplier.A
            );
        }

        MusicLabel.Position = _labelBasePosition;
        MusicLabel.Modulate = _labelBaseModulate;
    }

    private void TriggerFlash()
    {
        if (_hasFlashed) return;
        _hasFlashed = true;

        _flashTween?.Kill();
        _flashTween = CreateTween();

        FlashOverlay.Modulate = Colors.White;
        float flashIntensity = _rng.RandfRange(0.7f, 1.0f);

        _flashTween.TweenProperty(FlashOverlay.Material, "shader_parameter/progress", flashIntensity, 0.04f);
        _flashTween.Chain().TweenProperty(FlashOverlay.Material, "shader_parameter/progress", 0.0f, 0.25f);

        if (TargetSprites.Count > 0)
        {
            _baseTransforms[TargetSprites[0]].Position = new Vector2(343.95f, 270.037f);
            _baseTransforms[TargetSprites[0]].Scale = new Vector2(2.359f, 1.496f);
        }
        if (TargetSprites.Count > 1)
        {
            _baseTransforms[TargetSprites[1]].Position = new Vector2(254.707f, 90.544f);
            _baseTransforms[TargetSprites[1]].Scale = new Vector2(1.015f, 1.015f);
        }
        if (TargetSprites.Count > 2)
        {
            _baseTransforms[TargetSprites[2]].Position = new Vector2(423.128f, 135.501f);
            _baseTransforms[TargetSprites[2]].Scale = new Vector2(1.014f, 1.014f);
        }
        if (TargetSprites.Count > 3)
        {
            _baseTransforms[TargetSprites[3]].Position = new Vector2(342.608f, 233.467f);
            _baseTransforms[TargetSprites[3]].Scale = new Vector2(0.999f, 0.999f);
        }
        if (TargetSprites.Count > 4)
        {
            _baseTransforms[TargetSprites[4]].Position = new Vector2(341.081f, 322.73f);
            _baseTransforms[TargetSprites[4]].Scale = new Vector2(1.007f, 1.007f);
        }

        TriggerImpactOnAllSprites();
        AnimateLabelPostFlash();
        AnimatePostFlashBackgrounds();
    }

    private void AnimatePostFlashBackgrounds()
    {
        float animDuration = 2.0f;

        VideoBg1.Position = _videoBg1OriginalPos + new Vector2(OffscreenDistance, 0);
        Tween bg1Tween = CreateTween();
        bg1Tween.TweenProperty(VideoBg1, "position", _videoBg1OriginalPos, animDuration)
                .SetDelay(0.25f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);

        VideoBg2.Position = _videoBg2OriginalPos + new Vector2(OffscreenDistance, 0);
        Tween bg2Tween = CreateTween();
        bg2Tween.TweenProperty(VideoBg2, "position", _videoBg2OriginalPos, animDuration)
                .SetDelay(0.50f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);

        LogoBgCircle.Position = _logoBgCircleOriginalPos - new Vector2(0, OffscreenDistance);
        Tween logoTween = CreateTween();
        logoTween.TweenProperty(LogoBgCircle, "position", _logoBgCircleOriginalPos, animDuration)
                 .SetDelay(0.75f)
                 .SetEase(Tween.EaseType.Out)
                 .SetTrans(Tween.TransitionType.Back);

        float spinDir = _rng.RandiRange(0, 1) == 0 ? 1f : -1f;
        Tween spinTween = CreateTween().SetLoops();
        spinTween.TweenProperty(LogoBgCircle, "rotation", Mathf.Tau * spinDir, 3.0f)
                 .AsRelative();
    }

    private void OnAudioFinished()
    {
        GetTree().Quit();
    }
}
