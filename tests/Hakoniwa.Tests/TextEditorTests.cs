using Hakoniwa.Core;
using Xunit;

namespace Hakoniwa.Tests;

public sealed class TextEditorTests
{
    [Fact]
    public void Insert_TypesAtCaret_NotAtEnd()
    {
        var editor = new TextEditor();
        editor.Bind("你好世界");
        editor.Click(2, select: false);
        editor.Insert("，");
        Assert.Equal("你好，世界", editor.Text);
        Assert.Equal(3, editor.Caret);
    }

    [Fact]
    public void Selection_ReplaceAndCopyRange()
    {
        var editor = new TextEditor();
        editor.Bind("abcdef");
        editor.Click(1, select: false);
        editor.Click(4, select: true);
        Assert.Equal("bcd", editor.Selected);
        editor.Insert("X");
        Assert.Equal("aXef", editor.Text);
        Assert.Equal(2, editor.Caret);
        Assert.False(editor.HasSelection);
    }

    [Fact]
    public void UndoRedo_RestoresTextAndCaret()
    {
        var editor = new TextEditor();
        editor.Bind("ab");
        editor.Insert("c");
        editor.Backspace();
        editor.Undo();
        Assert.Equal("abc", editor.Text);
        editor.Redo();
        Assert.Equal("ab", editor.Text);
    }

    [Fact]
    public void WordMove_SkipsSpaces()
    {
        var editor = new TextEditor();
        editor.Bind("foo  bar");
        editor.Click(0, select: false);
        editor.Move(1, select: false, word: true);
        Assert.Equal(5, editor.Caret);
        editor.Move(1, select: false, word: true);
        Assert.Equal(8, editor.Caret);
        editor.Move(-1, select: false, word: true);
        Assert.Equal(5, editor.Caret);
    }

    [Fact]
    public void WordMove_CjkIsOneCharacter()
    {
        var editor = new TextEditor();
        editor.Bind("你好世界");
        editor.Move(-1, select: false, word: true);
        Assert.Equal(3, editor.Caret);
        editor.Move(-1, select: false, word: true);
        Assert.Equal(2, editor.Caret);
        editor.Move(1, select: false, word: true);
        Assert.Equal(3, editor.Caret);
    }

    [Fact]
    public void WordMove_ItemTagIsAtomic()
    {
        var editor = new TextEditor();
        editor.Bind("[i:1]hi");
        editor.Click(0, select: false);
        editor.Move(1, select: false, word: true);
        Assert.Equal(5, editor.Caret);
        editor.Move(1, select: false, word: true);
        Assert.Equal(7, editor.Caret);
        editor.Move(-1, select: false, word: true);
        Assert.Equal(5, editor.Caret);
        editor.Move(-1, select: false, word: true);
        Assert.Equal(0, editor.Caret);
    }

    [Fact]
    public void DeleteWord_RemovesPreviousRun()
    {
        var editor = new TextEditor();
        editor.Bind("foo 你好");
        editor.DeleteWord(-1);
        Assert.Equal("foo 你", editor.Text);
        editor.DeleteWord(-1);
        Assert.Equal("foo ", editor.Text);
        editor.DeleteWord(-1);
        Assert.Equal("", editor.Text);
    }
}
