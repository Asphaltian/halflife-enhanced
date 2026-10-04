using System.Text;

namespace GoldSrc.Vgui1;

public class FlowLayout : Layout
{
	private readonly int _hgap;

	public FlowLayout( int hgap )
	{
		_hgap = hgap;
	}

	public override void PerformLayout( Panel panel )
	{
		var x = 0;

		for ( var i = 0; i < panel.GetChildCount(); i++ )
		{
			var child = panel.GetChild( i );

			child.GetBounds( out _, out var y, out var wide, out _ );
			child.SetPos( x, y );

			x += wide + _hgap;
		}
	}
}

public class TextEntry : Panel, InputSignal
{
	private sealed class FocusHandler( TextEntry textEntry ) : FocusChangeSignal
	{
		public void FocusChanged( bool lost, Panel panel )
		{
			textEntry.ResetCursorBlink();
			textEntry.DoSelectNone();
		}
	}

	private const int DefaultCursorBlinkRate = 400;
	private const int CopyBufferLength = 200;
	private const int PasteBufferLength = 256;
	private const int TextOffset = 3;
	private const char HiddenChar = '*';

	protected readonly Dar<char> _lineDar = new();
	protected int _cursorPos;
	protected bool _cursorBlink;
	protected bool _hideText;
	protected long _cursorNextBlinkTime;
	protected int _cursorBlinkRate;
	protected int _selectStart;
	protected int _selectEnd;
	protected readonly List<ActionSignal> _actionSignalDar = [];
	protected Font _font;

	public TextEntry( string text, int x, int y, int wide, int tall ) : base( x, y, wide, tall )
	{
		_font = null;
		_hideText = false;
		_cursorPos = 0;
		_cursorBlinkRate = DefaultCursorBlinkRate;
		_selectStart = -1;
		_selectEnd = -1;

		ResetCursorBlink();
		SetText( text, text.Length );
		DoGotoEndOfLine();
		AddInputSignal( this );
		AddFocusChangeSignal( new FocusHandler( this ) );
	}

	public virtual void SetText( string text, int textLen )
	{
		_lineDar.SetCount( 0 );

		if ( text is not null )
		{
			_lineDar.EnsureCapacity( textLen );

			for ( var i = 0; i < textLen; i++ )
			{
				_lineDar.AddElement( text[i] );
				SetCharAt( text[i], i );
			}
		}

		DoGotoEndOfLine();
	}

	public virtual string GetText( int offset, int bufLen )
	{
		var text = new StringBuilder();

		for ( var i = offset; i < bufLen - 1; i++ )
		{
			if ( i >= _lineDar.GetCount() )
				break;

			text.Append( _lineDar[i] );
		}

		return text.ToString();
	}

	public virtual void ResetCursorBlink()
	{
		_cursorBlink = false;
		_cursorNextBlinkTime = GetApp().GetTimeMillis() + _cursorBlinkRate;
	}

	public virtual void DoGotoLeft()
	{
		SelectCheck();

		_cursorPos--;

		if ( _cursorPos < 0 )
			_cursorPos = 0;

		ResetCursorBlink();
		Repaint();
	}

	public virtual void DoGotoRight()
	{
		SelectCheck();

		_cursorPos++;

		if ( _cursorPos > _lineDar.GetCount() )
			_cursorPos = _lineDar.GetCount();

		ResetCursorBlink();
		Repaint();
	}

	public virtual void DoGotoFirstOfLine()
	{
		SelectCheck();

		_cursorPos = 0;

		ResetCursorBlink();
		Repaint();
	}

	public virtual void DoGotoEndOfLine()
	{
		SelectCheck();

		_cursorPos = _lineDar.GetCount();

		ResetCursorBlink();
		Repaint();
	}

	public virtual void DoInsertChar( char ch )
	{
		_lineDar.SetCount( _lineDar.GetCount() + 1 );

		for ( var i = _lineDar.GetCount() - 1; i >= _cursorPos; i-- )
			SetCharAt( _lineDar[i], i + 1 );

		SetCharAt( ch, _cursorPos );

		_cursorPos++;

		ResetCursorBlink();
		Repaint();
	}

	public virtual void DoBackspace()
	{
		if ( _cursorPos == 0 || _lineDar.GetCount() == 0 )
			return;

		for ( var i = _cursorPos; i < _lineDar.GetCount(); i++ )
			SetCharAt( _lineDar[i], i - 1 );

		_lineDar.SetCount( _lineDar.GetCount() - 1 );
		_cursorPos--;

		ResetCursorBlink();
		Repaint();
	}

	public virtual void DoDelete()
	{
		if ( _lineDar.GetCount() == 0 || _cursorPos == _lineDar.GetCount() )
			return;

		for ( var i = _cursorPos + 1; i < _lineDar.GetCount(); i++ )
			SetCharAt( _lineDar[i], i - 1 );

		_lineDar.SetCount( _lineDar.GetCount() - 1 );

		ResetCursorBlink();
		Repaint();
	}

	public virtual void DoSelectNone()
	{
		_selectStart = -1;

		Repaint();
	}

	public virtual void DoCopySelected()
	{
		if ( !GetSelectedRange( out var x0, out var x1 ) )
			return;

		var text = new StringBuilder();

		for ( var i = x0; i < x1 && text.Length < CopyBufferLength - 1; i++ )
			text.Append( _lineDar[i] );

		GetApp().SetClipboardText( text.ToString(), text.Length );
	}

	public virtual void DoPaste()
	{
		var text = GetApp().GetClipboardText( 0, PasteBufferLength, out var count );

		for ( var i = 0; i < count - 1; i++ )
			DoInsertChar( i < text.Length ? text[i] : '\0' );
	}

	public virtual void DoPasteSelected()
	{
		DoDeleteSelected();
		DoPaste();
	}

	public virtual void DoDeleteSelected()
	{
	}

	public virtual void AddActionSignal( ActionSignal s )
	{
		if ( !_actionSignalDar.Contains( s ) )
			_actionSignalDar.Add( s );
	}

	public virtual void SetFont( Font font )
	{
		_font = font;
	}

	public virtual void SetTextHidden( bool hideText )
	{
		_hideText = hideText;

		Repaint();
	}

	private Font CurrentFont() => _font ?? GetApp().GetScheme().GetFont( SchemeFont.Primary1 );

	public override void PaintBackground()
	{
		var font = CurrentFont();
		var tall = font.Tall;

		if ( GetSelectedPixelRange( out var x0, out var x1 ) )
		{
			DrawSetColor( SchemeColor.White );
			DrawFilledRect( 0, 0, x0, tall + 1 );
			DrawFilledRect( x1, 0, _wide, tall + 1 );
			DrawSetColor( 0, 0, 200, 0 );
			DrawFilledRect( x0, 0, x1, tall + 1 );
		}
		else
		{
			DrawSetColor( SchemeColor.White );
			DrawFilledRect( 0, 0, _wide, _tall );
		}

		DrawSetTextFont( font );
		DrawSetTextColor( SchemeColor.Black );
		DrawSetTextPos( TextOffset, 0 );

		for ( var i = 0; i < _lineDar.GetCount(); i++ )
			DrawPrintChar( _hideText ? HiddenChar : _lineDar[i] );

		if ( !HasFocus() )
			return;

		DrawSetColor( SchemeColor.Black );
		DrawFilledRect( 0, 0, _wide, 1 );
		DrawFilledRect( 0, _tall - 1, _wide, _tall );
		DrawFilledRect( 0, 1, 1, _tall - 1 );
		DrawFilledRect( _wide - 1, 1, _wide, _tall - 1 );

		if ( _cursorBlink )
			return;

		var x = CursorToPixelSpace( _cursorPos );

		DrawSetColor( SchemeColor.Black );
		DrawFilledRect( x + TextOffset, 2, x + TextOffset + 1, tall - 1 );
	}

	protected virtual void SetCharAt( char ch, int index )
	{
		if ( index < 0 )
			return;

		_lineDar.EnsureCapacity( index + 1 );
		_lineDar.SetElementAt( ch, index );
	}

	protected virtual void FireActionSignal()
	{
		for ( var i = 0; i < _actionSignalDar.Count; i++ )
			_actionSignalDar[i].ActionPerformed( this );
	}

	protected virtual bool GetSelectedRange( out int cx0, out int cx1 )
	{
		cx0 = 0;
		cx1 = 0;

		if ( _selectStart == -1 )
			return false;

		cx0 = _selectStart;
		cx1 = _selectEnd;

		if ( cx1 < cx0 )
			(cx0, cx1) = (cx1, cx0);

		return true;
	}

	protected virtual bool GetSelectedPixelRange( out int cx0, out int cx1 )
	{
		if ( !GetSelectedRange( out cx0, out cx1 ) )
			return false;

		cx0 = CursorToPixelSpace( cx0 );
		cx1 = CursorToPixelSpace( cx1 );

		return true;
	}

	protected virtual int CursorToPixelSpace( int cursorPos )
	{
		var font = CurrentFont();
		var x = 0;

		for ( var i = 0; i < _lineDar.GetCount(); i++ )
		{
			if ( i == cursorPos )
				return x;

			font.GetCharAbcWide( _hideText ? HiddenChar : (int)(sbyte)_lineDar[i], out var a, out var b, out var c );

			x += a + b + c;
		}

		return x;
	}

	protected virtual void SelectCheck()
	{
		if ( !IsKeyDown( KeyCode.LShift ) && !IsKeyDown( KeyCode.RShift ) )
		{
			_selectStart = -1;
			return;
		}

		if ( _selectStart == -1 )
			_selectStart = _cursorPos;
	}

	public virtual void CursorMoved( int x, int y, Panel panel )
	{
	}

	public virtual void CursorEntered( Panel panel )
	{
	}

	public virtual void CursorExited( Panel panel )
	{
	}

	public virtual void MousePressed( MouseCode code, Panel panel )
	{
		ResetCursorBlink();
		RequestFocus();
		Repaint();
	}

	public virtual void MouseDoublePressed( MouseCode code, Panel panel )
	{
	}

	public virtual void MouseReleased( MouseCode code, Panel panel )
	{
	}

	public virtual void MouseWheeled( int delta, Panel panel )
	{
	}

	public virtual void KeyPressed( KeyCode code, Panel panel )
	{
	}

	public virtual void KeyTyped( KeyCode code, Panel panel )
	{
		var shift = IsKeyDown( KeyCode.LShift ) || IsKeyDown( KeyCode.RShift );
		var ctrl = IsKeyDown( KeyCode.LControl ) || IsKeyDown( KeyCode.RControl );

		if ( ctrl )
		{
			if ( code == KeyCode.C )
				DoCopySelected();
			else if ( code == KeyCode.V )
				DoPaste();
		}
		else
		{
			switch ( code )
			{
				case KeyCode.Enter:
				case KeyCode.Tab:
				case KeyCode.LShift:
				case KeyCode.RShift:
					break;
				case KeyCode.BackSpace:
					DoBackspace();
					break;
				case KeyCode.Insert:
					if ( shift )
						DoPaste();

					break;
				case KeyCode.Delete:
					if ( shift )
						DoDeleteSelected();
					else
						DoDelete();

					break;
				case KeyCode.Home:
					DoGotoFirstOfLine();
					break;
				case KeyCode.End:
					DoGotoEndOfLine();
					break;
				case KeyCode.Left:
					DoGotoLeft();
					break;
				case KeyCode.Right:
					DoGotoRight();
					break;
				default:
					var ch = GetApp().GetKeyCodeChar( code, IsKeyDown( KeyCode.LShift ) || IsKeyDown( KeyCode.RShift ) );

					if ( ch != '\0' )
						DoInsertChar( ch );

					break;
			}
		}

		_selectEnd = _cursorPos;

		if ( code == KeyCode.Enter )
			FireActionSignal();
	}

	public virtual void KeyReleased( KeyCode code, Panel panel )
	{
	}

	public virtual void KeyFocusTicked( Panel panel )
	{
		var time = GetApp().GetTimeMillis();

		if ( _cursorNextBlinkTime >= time )
			return;

		_cursorBlink = !_cursorBlink;
		_cursorNextBlinkTime = _cursorBlinkRate + time;

		Repaint();
	}
}
