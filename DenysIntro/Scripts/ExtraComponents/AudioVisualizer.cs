using Godot;

public partial class AudioVisualizer : Control
{
    [Export]
    public AudioStreamPlayer AudioPlayer { get; set; } = default!;

    private int _busIndex = 0;
    private AudioEffectSpectrumAnalyzerInstance? _spectrumInstance;
    private const int BarCount = 128;
    private float[] _barHeights = new float[BarCount];
    private RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _rng.Randomize();
        AnchorLeft = 0f;
        AnchorRight = 1f;
        AnchorTop = 1f;
        AnchorBottom = 1f;
        OffsetTop = -300f;
        OffsetBottom = 0f;
        OffsetLeft = 0f;
        OffsetRight = 0f;
        MouseFilter = MouseFilterEnum.Ignore;

        _busIndex = AudioServer.GetBusIndex(AudioPlayer.Bus);

        bool hasSpectrum = false;
        int effectCount = AudioServer.GetBusEffectCount(_busIndex);
        for (int i = 0; i < effectCount; i++)
        {
            if (AudioServer.GetBusEffect(_busIndex, i) is AudioEffectSpectrumAnalyzer)
            {
                _spectrumInstance = (AudioEffectSpectrumAnalyzerInstance)AudioServer.GetBusEffectInstance(_busIndex, i);
                hasSpectrum = true;
                break;
            }
        }

        if (!hasSpectrum)
        {
            var spectrumEffect = new AudioEffectSpectrumAnalyzer();
            AudioServer.AddBusEffect(_busIndex, spectrumEffect);
            _spectrumInstance = (AudioEffectSpectrumAnalyzerInstance)AudioServer.GetBusEffectInstance(_busIndex, effectCount);
        }
    }

    public override void _Process(double delta)
    {
        if (AudioPlayer.Playing && _spectrumInstance != null)
        {
            float minFreq = 0f;
            float maxFreq = 10000f;
            float freqRange = (maxFreq - minFreq) / BarCount;

            for (int i = 0; i < BarCount; i++)
            {
                float f1 = minFreq + i * freqRange;
                float f2 = f1 + freqRange;
                var magnitude = _spectrumInstance.GetMagnitudeForFrequencyRange(f1, f2);

                float energy = Mathf.Clamp((60.0f + Mathf.LinearToDb(magnitude.Length())) / 60.0f, 0.0f, 1.0f);
                _barHeights[i] = Mathf.Lerp(_barHeights[i], energy, (float)(delta * 15.0f));
            }
        }
        else
        {
            for (int i = 0; i < BarCount; i++)
            {
                float target = AudioPlayer.Playing ? _rng.RandfRange(0.1f, 0.3f) : 0f;
                _barHeights[i] = Mathf.Lerp(_barHeights[i], target, (float)(delta * 5.0f));
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 size = Size;
        if (size.X <= 0 || size.Y <= 0) return;

        float barWidth = size.X / BarCount;
        float spacing = 3f;

        Color barColor = new Color(0.48f, 0.2f, 0.8f, 0.5f);

        for (int i = 0; i < BarCount; i++)
        {
            float h = _barHeights[i] * size.Y * 1.3f;
            float x = i * barWidth + spacing * 0.5f;
            float w = Mathf.Max(1f, barWidth - spacing);
            float y = size.Y - h;

            DrawRect(new Rect2(x, y, w, h), barColor);
        }
    }
}
