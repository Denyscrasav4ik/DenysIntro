using Godot;
using System.Collections.Generic;

public partial class IntroAnimator : Node
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
    public Control DayLabel { get; set; } = default!;

    [Export]
    public Control TitleLabel { get; set; } = default!;

    [Export]
    public Texture2D NewTitleTexture { get; set; } = default!;

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
    private bool _labelHasExited = false;

    private Vector2 _dayLabelOriginalPos;
    private Vector2 _dayLabelBasePos;
    private Color _dayLabelOriginalModulate;
    private Color _dayLabelBaseModulate;

    private Vector2 _titleLabelOriginalPos;
    private Vector2 _titleLabelBasePos;
    private Color _titleLabelOriginalModulate;
    private Color _titleLabelBaseModulate;

    private Tween? _flashTween;
    private RandomNumberGenerator _rng = new();

    private int _audioBusIndex = 0;
    private bool _hasFlashed = false;

    public override void _Ready()
    {
        _rng.Randomize();

        Color transparent = FlashOverlay.Modulate;
        transparent.A = 0;
        FlashOverlay.Modulate = transparent;

        Tween flashDelayTween = CreateTween();
        flashDelayTween.TweenInterval(FlashDelay);
        flashDelayTween.TweenCallback(Callable.From(TriggerFlash));

        _labelOriginalPosition = MusicLabel.Position;
        _labelBasePosition = MusicLabel.Position;
        _labelOriginalModulate = MusicLabel.Modulate;
        _labelBaseModulate = MusicLabel.Modulate;

        if (DayLabel != null)
        {
            _dayLabelOriginalPos = DayLabel.Position;
            _dayLabelOriginalModulate = DayLabel.Modulate;

            Vector2 startPos = _dayLabelOriginalPos;
            bool fromLeft = _rng.RandiRange(0, 1) == 0;
            startPos.X += fromLeft ? -OffscreenDistance : OffscreenDistance;

            Color startColor = _dayLabelOriginalModulate;
            startColor.A = 0f;

            _dayLabelBasePos = startPos;
            _dayLabelBaseModulate = startColor;
        }

        if (TitleLabel != null)
        {
            _titleLabelOriginalPos = TitleLabel.Position;
            _titleLabelOriginalModulate = TitleLabel.Modulate;

            Vector2 startPos = _titleLabelOriginalPos;
            bool fromLeft = _rng.RandiRange(0, 1) == 0;
            startPos.X += fromLeft ? -OffscreenDistance : OffscreenDistance;

            Color startColor = _titleLabelOriginalModulate;
            startColor.A = 0f;

            _titleLabelBasePos = startPos;
            _titleLabelBaseModulate = startColor;
        }

        AnimateLabelIn();

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
    }

    public override void _Process(double delta)
    {
        if (!_hasFlashed && AudioPlayer.Playing)
        {
            float peakLeft = AudioServer.GetBusPeakVolumeLeftDb(_audioBusIndex, 0);
            float peakRight = AudioServer.GetBusPeakVolumeRightDb(_audioBusIndex, 0);
            float currentDb = Mathf.Max(peakLeft, peakRight);

            if (currentDb >= PeakDbThreshold)
            {
                TriggerImpactOnAllSprites();
            }
        }
        ApplyCombinedTransforms();
    }

    private void AnimateLabelIn()
    {
        Vector2 startPos = _labelOriginalPosition;
        int direction = _rng.RandiRange(0, 1);
        if (direction == 0)
        {
            startPos.Y -= OffscreenDistance;
        }
        else
        {
            startPos.X += OffscreenDistance;
        }

        Color startColor = _labelOriginalModulate;
        startColor.A = 0;

        _labelBasePosition = startPos;
        _labelBaseModulate = startColor;

        Tween tween = CreateTween();
        tween.SetParallel(true);
        tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart);

        tween.TweenMethod(Callable.From<Vector2>(v => _labelBasePosition = v), startPos, _labelOriginalPosition, 1.0f);
        tween.TweenMethod(Callable.From<Color>(c => _labelBaseModulate = c), startColor, _labelOriginalModulate, 1.0f);
    }

    private void AnimateLabelOut()
    {
        if (_labelHasExited) return;
        _labelHasExited = true;

        Vector2 targetOutPos = _labelOriginalPosition;
        int direction = _rng.RandiRange(0, 1);
        if (direction == 0)
        {
            targetOutPos.Y -= OffscreenDistance;
        }
        else
        {
            targetOutPos.X += OffscreenDistance;
        }

        Color endColor = _labelOriginalModulate;
        endColor.A = 0;

        Tween tween = CreateTween();
        tween.SetParallel(true);
        tween.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quart);

        tween.TweenMethod(Callable.From<Vector2>(v => _labelBasePosition = v), _labelBasePosition, targetOutPos, 1.0f);
        tween.TweenMethod(Callable.From<Color>(c => _labelBaseModulate = c), _labelBaseModulate, endColor, 1.0f);
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

    private void TriggerImpactOnAllSprites()
    {
        if (_hasFlashed) return;

        foreach (var sprite in TargetSprites)
        {
            ApplyImpact(sprite);
        }
    }

    private void ApplyImpact(Sprite2D sprite)
    {
        if (_hasFlashed) return;

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

        if (DayLabel != null)
        {
            DayLabel.Position = _dayLabelBasePos;
            DayLabel.Modulate = _dayLabelBaseModulate;
        }

        if (TitleLabel != null)
        {
            TitleLabel.Position = _titleLabelBasePos;
            TitleLabel.Modulate = _titleLabelBaseModulate;
        }
    }

    private void TriggerFlash()
    {
        if (_hasFlashed) return;
        _hasFlashed = true;

        _flashTween?.Kill();
        _flashTween = CreateTween();
        float flashAlpha = _rng.RandfRange(0.7f, 1.0f);
        _flashTween.TweenProperty(FlashOverlay, "modulate:a", flashAlpha, 0.04f);
        _flashTween.Chain().TweenProperty(FlashOverlay, "modulate:a", 0.0f, 0.25f);

        Tween labelExitDelayTween = CreateTween();
        labelExitDelayTween.TweenInterval(1.0f);
        labelExitDelayTween.TweenCallback(Callable.From(AnimateLabelOut));

        labelExitDelayTween.TweenCallback(Callable.From(AnimateSpritesAfterFlash));
    }

    private void AnimateSpritesAfterFlash()
    {
        if (TargetSprites.Count > 0)
        {
            Sprite2D sprite0 = TargetSprites[0];
            var baseTr0 = _baseTransforms[sprite0];

            Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
            Vector2 screenCenter = viewportSize / 2f;

            Vector2 targetScale = baseTr0.Scale;
            if (sprite0.Texture != null)
            {
                Vector2 texSize = sprite0.Texture.GetSize();
                targetScale = new Vector2(1280f / texSize.X, 720f / texSize.Y);
            }

            Tween tween0 = CreateTween();
            tween0.SetParallel(true);
            tween0.SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);

            tween0.TweenMethod(Callable.From<Vector2>(v => baseTr0.Position = v), baseTr0.Position, screenCenter, 1.0f);
            tween0.TweenMethod(Callable.From<Vector2>(s => baseTr0.Scale = s), baseTr0.Scale, targetScale, 1.0f);
        }

        Tween? lastSpriteExitTween = null;

        for (int i = 1; i <= 3; i++)
        {
            if (i >= TargetSprites.Count) break;

            Sprite2D sprite = TargetSprites[i];
            var baseTr = _baseTransforms[sprite];

            Vector2 targetInPos = Vector2.Zero;
            float targetRotation = baseTr.Rotation;
            Vector2 targetScale = baseTr.Scale;
            float targetSkew = baseTr.Skew;

            if (i == 1)
            {
                targetInPos = new Vector2(902f, 84f);
                targetRotation = 0f;
            }
            else if (i == 2)
            {
                targetInPos = new Vector2(1231f, 84f);
            }
            else if (i == 3)
            {
                targetInPos = new Vector2(1631f, 84f);
                targetScale = new Vector2(1.39f, 1.39f);
                targetSkew = Mathf.DegToRad(-21f);
            }

            int direction = _rng.RandiRange(0, 1);
            Vector2 targetOutPos = baseTr.Position;
            if (direction == 0)
            {
                targetOutPos.Y -= OffscreenDistance;
            }
            else
            {
                targetOutPos.X += OffscreenDistance;
            }

            Color endColor = baseTr.Modulate;
            endColor.A = 0f;

            float rotationAmount = Mathf.Pi * 2;
            int rotDirection = _rng.RandiRange(0, 1) == 0 ? 1 : -1;
            float endRotation = baseTr.Rotation + (rotationAmount * rotDirection);

            Tween exitTween = CreateTween();
            exitTween.SetParallel(true);
            exitTween.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quart);

            exitTween.TweenMethod(Callable.From<Vector2>(v => baseTr.Position = v), baseTr.Position, targetOutPos, 1.0f);
            exitTween.TweenMethod(Callable.From<Color>(c => baseTr.Modulate = c), baseTr.Modulate, endColor, 1.0f);
            exitTween.TweenMethod(Callable.From<Vector2>(s => baseTr.Scale = s), baseTr.Scale, Vector2.Zero, 1.0f);
            exitTween.TweenMethod(Callable.From<float>(r => baseTr.Rotation = r), baseTr.Rotation, endRotation, 1.0f);

            exitTween.Chain().TweenInterval(1.0f);

            int capturedDirection = direction;
            Vector2 capturedTargetInPos = targetInPos;
            float capturedTargetRotation = targetRotation;
            Vector2 capturedTargetScale = targetScale;
            float capturedTargetSkew = targetSkew;

            exitTween.Chain().TweenCallback(Callable.From(() =>
            {
                Vector2 readyPos = baseTr.Position;
                if (capturedDirection == 0)
                {
                    readyPos.X = capturedTargetInPos.X;
                }
                else
                {
                    readyPos.Y = capturedTargetInPos.Y;
                }
                baseTr.Position = readyPos;

                Color targetColor = _originalTransforms[sprite].Modulate;
                targetColor.A = 1.0f;

                Tween enterTween = CreateTween();
                enterTween.SetParallel(true);
                enterTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart);

                enterTween.TweenMethod(Callable.From<Vector2>(v => baseTr.Position = v), readyPos, capturedTargetInPos, 1.0f);
                enterTween.TweenMethod(Callable.From<Color>(c => baseTr.Modulate = c), baseTr.Modulate, targetColor, 1.0f);
                enterTween.TweenMethod(Callable.From<float>(r => baseTr.Rotation = r), baseTr.Rotation, capturedTargetRotation, 1.0f);
                enterTween.TweenMethod(Callable.From<Vector2>(s => baseTr.Scale = s), Vector2.Zero, capturedTargetScale, 1.0f);
                enterTween.TweenMethod(Callable.From<float>(sk => baseTr.Skew = sk), baseTr.Skew, capturedTargetSkew, 1.0f);
            }));

            lastSpriteExitTween = exitTween;
        }

        if (lastSpriteExitTween != null)
        {
            lastSpriteExitTween.Chain().TweenInterval(1.5f);
            lastSpriteExitTween.Chain().TweenCallback(Callable.From(AnimateDayAndTitleLabelsIn));
        }

        if (TargetSprites.Count > 4)
        {
            Sprite2D sprite4 = TargetSprites[4];
            var baseTr4 = _baseTransforms[sprite4];

            Vector2 targetOutPos = baseTr4.Position;
            int direction = _rng.RandiRange(0, 3);
            switch (direction)
            {
                case 0: targetOutPos.Y -= OffscreenDistance; break;
                case 1: targetOutPos.X += OffscreenDistance; break;
                case 2: targetOutPos.Y += OffscreenDistance; break;
                case 3: targetOutPos.X -= OffscreenDistance; break;
            }

            Color endColor = baseTr4.Modulate;
            endColor.A = 0;

            float rotationAmount = Mathf.Pi * 2;
            int rotDirection = _rng.RandiRange(0, 1) == 0 ? 1 : -1;
            float endRotation = baseTr4.Rotation + (rotationAmount * rotDirection);

            Tween tween4 = CreateTween();
            tween4.SetParallel(true);
            tween4.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quart);

            tween4.TweenMethod(Callable.From<Vector2>(v => baseTr4.Position = v), baseTr4.Position, targetOutPos, 1.0f);
            tween4.TweenMethod(Callable.From<Color>(c => baseTr4.Modulate = c), baseTr4.Modulate, endColor, 1.0f);
            tween4.TweenMethod(Callable.From<Vector2>(s => baseTr4.Scale = s), baseTr4.Scale, Vector2.Zero, 1.0f);
            tween4.TweenMethod(Callable.From<float>(r => baseTr4.Rotation = r), baseTr4.Rotation, endRotation, 1.0f);
        }
    }

    private void AnimateDayAndTitleLabelsIn()
    {
        Tween dayTween = CreateTween();
        dayTween.SetParallel(true);
        dayTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart);

        dayTween.TweenMethod(Callable.From<Vector2>(v => _dayLabelBasePos = v), _dayLabelBasePos, _dayLabelOriginalPos, 1.0f);
        dayTween.TweenMethod(Callable.From<Color>(c => _dayLabelBaseModulate = c), _dayLabelBaseModulate, _dayLabelOriginalModulate, 1.0f);

        Tween titleTween = CreateTween();
        titleTween.SetParallel(true);
        titleTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart);

        titleTween.TweenMethod(Callable.From<Vector2>(v => _titleLabelBasePos = v), _titleLabelBasePos, _titleLabelOriginalPos, 1.0f);
        titleTween.TweenMethod(Callable.From<Color>(c => _titleLabelBaseModulate = c), _titleLabelBaseModulate, _titleLabelOriginalModulate, 1.0f);

        if (TargetSprites.Count > 0)
        {
            var sprite0 = TargetSprites[0];
            var baseTr0 = _baseTransforms[sprite0];

            Sprite2D bgSprite = new Sprite2D();
            bgSprite.Texture = NewTitleTexture;
            bgSprite.Material = sprite0.Material;
            bgSprite.Position = baseTr0.Position;
            bgSprite.Rotation = baseTr0.Rotation;

            Vector2 targetScale = baseTr0.Scale;
            if (NewTitleTexture != null)
            {
                Vector2 texSize = NewTitleTexture.GetSize();
                targetScale = new Vector2(1280f / texSize.X, 720f / texSize.Y);
            }
            bgSprite.Scale = targetScale;

            Color bgStartColor = baseTr0.Modulate;
            bgStartColor.A = 0f;
            bgSprite.Modulate = bgStartColor;

            Node parent = sprite0.GetParent();
            int sprite0Index = sprite0.GetIndex();
            parent.AddChild(bgSprite);
            parent.MoveChild(bgSprite, sprite0Index);

            Tween bgFadeTween = CreateTween();
            bgFadeTween.TweenProperty(bgSprite, "modulate:a", baseTr0.Modulate.A, 0.5f)
                       .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);

            Vector2 targetOutPos = baseTr0.Position;
            int direction = _rng.RandiRange(0, 3);
            switch (direction)
            {
                case 0: targetOutPos.Y -= OffscreenDistance; break;
                case 1: targetOutPos.X += OffscreenDistance; break;
                case 2: targetOutPos.Y += OffscreenDistance; break;
                case 3: targetOutPos.X -= OffscreenDistance; break;
            }

            Color endColor = baseTr0.Modulate;
            endColor.A = 0f;

            float rotationAmount = Mathf.Pi * 2;
            int rotDirection = _rng.RandiRange(0, 1) == 0 ? 1 : -1;
            float endRotation = baseTr0.Rotation + (rotationAmount * rotDirection);

            Tween sprite0ExitTween = CreateTween();
            sprite0ExitTween.SetParallel(true);
            sprite0ExitTween.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quart);

            sprite0ExitTween.TweenMethod(Callable.From<Vector2>(v => baseTr0.Position = v), baseTr0.Position, targetOutPos, 1.0f);
            sprite0ExitTween.TweenMethod(Callable.From<Color>(c => baseTr0.Modulate = c), baseTr0.Modulate, endColor, 1.0f);
            sprite0ExitTween.TweenMethod(Callable.From<Vector2>(s => baseTr0.Scale = s), baseTr0.Scale, Vector2.Zero, 1.0f);
            sprite0ExitTween.TweenMethod(Callable.From<float>(r => baseTr0.Rotation = r), baseTr0.Rotation, endRotation, 1.0f);
        }
    }

    private void OnAudioFinished()
    {
        GetTree().Quit();
    }
}
