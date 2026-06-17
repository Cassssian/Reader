using Microsoft.Maui.Graphics;

namespace Reader.Controls;

// Lightweight bar visualizer drawn with GraphicsView. It animates while Active is true.
// TTS engines don't expose live amplitude, so we render a smooth pseudo-waveform that
// reads as "speaking" — set Active to bind it to the play state.
public class AudioWaveView : GraphicsView, IDrawable
{
    readonly WaveDrawable _d = new();
    IDispatcherTimer? _timer;

    public static readonly BindableProperty ActiveProperty =
        BindableProperty.Create(nameof(Active), typeof(bool), typeof(AudioWaveView), false, propertyChanged: OnActive);

    public bool Active { get => (bool)GetValue(ActiveProperty); set => SetValue(ActiveProperty, value); }

    public AudioWaveView()
    {
        Drawable = _d;
        HeightRequest = 64;
    }

    static void OnActive(BindableObject o, object _, object n)
    {
        var v = (AudioWaveView)o;
        if ((bool)n) v.Start(); else v.Stop();
    }

    void Start()
    {
        _timer ??= Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(60);
        _timer.Tick -= Tick; _timer.Tick += Tick;
        _timer.Start();
    }

    void Stop() { _timer?.Stop(); _d.Idle(); Invalidate(); }

    void Tick(object? s, EventArgs e) { _d.Step(); Invalidate(); }

    public void Draw(ICanvas canvas, RectF rect) => _d.Draw(canvas, rect);

    class WaveDrawable : IDrawable
    {
        const int Bars = 28;
        readonly float[] _h = new float[Bars];
        readonly Random _r = new();
        double _phase;

        public void Step()
        {
            _phase += 0.35;
            for (int i = 0; i < Bars; i++)
            {
                // Sine envelope + jitter -> organic, speech-like motion.
                var target = 0.25f + 0.75f * (float)Math.Abs(Math.Sin(_phase + i * 0.4)) * (0.6f + 0.4f * (float)_r.NextDouble());
                _h[i] += (target - _h[i]) * 0.5f;
            }
        }

        public void Idle() { for (int i = 0; i < Bars; i++) _h[i] = 0.12f; }

        public void Draw(ICanvas g, RectF r)
        {
            float gap = 4, w = (r.Width - gap * (Bars - 1)) / Bars, cy = r.Center.Y;
            g.FillColor = Color.FromArgb("#6C5CE7");
            for (int i = 0; i < Bars; i++)
            {
                float h = Math.Max(3, _h[i] * r.Height), x = i * (w + gap);
                g.FillRoundedRectangle(x, cy - h / 2, w, h, w / 2);
            }
        }
    }
}
