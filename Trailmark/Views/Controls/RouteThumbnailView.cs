namespace Trailmark.Views.Controls;

public sealed class RouteThumbnailView : GraphicsView
{
    public static readonly BindableProperty RouteColorProperty = BindableProperty.Create(
        nameof(RouteColor), typeof(Color), typeof(RouteThumbnailView), Color.FromArgb("#658F35"),
        propertyChanged: (bindable, _, value) =>
        {
            var view = (RouteThumbnailView)bindable;
            view.Drawable = new RouteThumbnailDrawable((Color)value);
            view.Invalidate();
        });

    public Color RouteColor
    {
        get => (Color)GetValue(RouteColorProperty);
        set => SetValue(RouteColorProperty, value);
    }

    public RouteThumbnailView() => Drawable = new RouteThumbnailDrawable(RouteColor);

    private sealed class RouteThumbnailDrawable(Color routeColor) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
            canvas.FillColor = Color.FromArgb(dark ? "#20281F" : "#DCE5CE");
            canvas.FillRoundedRectangle(dirtyRect, 15);

            canvas.StrokeColor = Color.FromArgb(dark ? "#2E3A2C" : "#F7F4E9");
            canvas.StrokeSize = 5;
            canvas.DrawLine(-5, dirtyRect.Height * .78f, dirtyRect.Width + 5, dirtyRect.Height * .18f);
            canvas.DrawLine(dirtyRect.Width * .14f, -5, dirtyRect.Width * .82f, dirtyRect.Height + 5);

            var path = new PathF();
            path.MoveTo(dirtyRect.Width * .08f, dirtyRect.Height * .8f);
            path.CurveTo(dirtyRect.Width * .3f, dirtyRect.Height * .72f,
                dirtyRect.Width * .22f, dirtyRect.Height * .3f,
                dirtyRect.Width * .43f, dirtyRect.Height * .38f);
            path.CurveTo(dirtyRect.Width * .64f, dirtyRect.Height * .46f,
                dirtyRect.Width * .56f, dirtyRect.Height * .8f,
                dirtyRect.Width * .92f, dirtyRect.Height * .18f);
            canvas.StrokeColor = routeColor;
            canvas.StrokeSize = 5;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawPath(path);
        }
    }
}
