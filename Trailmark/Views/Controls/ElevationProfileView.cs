using System.Collections;

namespace Trailmark.Views.Controls;

public sealed class ElevationProfileView : GraphicsView
{
    public static readonly BindableProperty ValuesProperty = BindableProperty.Create(
        nameof(Values), typeof(IEnumerable), typeof(ElevationProfileView), null,
        propertyChanged: (bindable, _, value) =>
        {
            var view = (ElevationProfileView)bindable;
            view.Drawable = new ElevationDrawable(value as IEnumerable);
            view.Invalidate();
        });

    public IEnumerable? Values
    {
        get => (IEnumerable?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    private sealed class ElevationDrawable : IDrawable
    {
        private readonly double[] values;

        public ElevationDrawable(IEnumerable? source) => values = source?
            .Cast<object>()
            .Select(value => Convert.ToDouble(value))
            .Where(double.IsFinite)
            .ToArray() ?? [];

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (values.Length < 2)
                return;

            double min = values.Min();
            double range = Math.Max(1, values.Max() - min);
            float bottom = dirtyRect.Bottom - 3;
            var line = new PathF();
            var area = new PathF();

            for (int index = 0; index < values.Length; index++)
            {
                float x = dirtyRect.Left + index * dirtyRect.Width / (values.Length - 1);
                float y = bottom - (float)((values[index] - min) / range) * (dirtyRect.Height - 10);
                if (index == 0)
                {
                    line.MoveTo(x, y);
                    area.MoveTo(x, bottom);
                    area.LineTo(x, y);
                }
                else
                {
                    line.LineTo(x, y);
                    area.LineTo(x, y);
                }
            }

            area.LineTo(dirtyRect.Right, bottom);
            area.Close();
            canvas.FillColor = Color.FromArgb("#60B5DB68");
            canvas.FillPath(area);
            canvas.StrokeColor = Color.FromArgb("#658F35");
            canvas.StrokeSize = 3;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.DrawPath(line);
        }
    }
}
