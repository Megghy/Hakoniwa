namespace Hakoniwa.UI.Windows;

public interface IWindow
{
    string Title { get; }
    string Label { get; }
    bool IsOpen { get; set; }
    void Draw();
}
