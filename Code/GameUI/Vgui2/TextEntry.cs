using System;
using System.Globalization;

namespace GoldSrc.GameUI;

public class TextEntry : Panel
{
	private const int DrawOffsetX = 3;
	private const int DrawOffsetY = 1;
	private const int BufferSize = 999999;
	private const int EllipsisDots = 3;
	private const int CursorBlinkRate = 400;
	private const int ScrollBarSize = 18;
	private const int ProportionalBaseWide = 640;
	private const int BackspaceScroll = 6;
	private const int MouseWheelLines = 3;
	private const string WarningSound = "Resource\\warning.wav";

	private static bool _showKeyboardToggle;

	private readonly List<char> _textStream = [];
	private readonly List<char> _undoTextStream = [];
	private readonly List<int> _lineBreaks = [];
	private readonly int _scrollBarSize;

	private int _cursorPos;
	private bool _cursorIsAtEnd;
	private bool _putCursorAtEnd;
	private int _undoCursorPos;
	private bool _cursorBlink;
	private bool _hideText;
	private bool _editable;
	private bool _mouseSelection;
	private bool _mouseDragSelection;
	private int _cursorNextBlinkTime;
	private int _selectStart = -1;
	private int _selectEnd = -1;
	private int _pixelsIndent;
	private int _maxCharCount = -1;
	private SchemeFont _font;
	private bool _dataChanged;
	private bool _multiline;
	private bool _verticalScrollbar;
	private ScrollBar _vertScrollBar;
	private Color32 _cursorColor;
	private Color32 _disabledFgColor;
	private Color32 _disabledBgColor;
	private Color32 _selectionColor;
	private Color32 _selectionTextColor;
	private Color32 _defaultSelectionBG2Color;
	private int _currentStartLine;
	private int _currentStartIndex;
	private bool _horizScrollingAllowed = true;
	private bool _catchEnterKey;
	private bool _wrap;
	private bool _sendNewLines;
	private int _drawWidth;
	private Menu _editMenu;
	private int _recalculateBreaksIndex;
	private bool _selectAllOnFirstFocus;
	private bool _allowNumericInputOnly;
	private bool _allowNonAsciiCharacters;
	private bool _autoProgressOnHittingCharLimit;
	private bool _useFallbackFont;
	private SchemeFont _fallbackFont;

	public TextEntry( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name )
	{
		_cursorNextBlinkTime = Vgui.TimeMillis + CursorBlinkRate;

		SetCursor( CursorCode.IBeam );

		GotoTextEnd();

		_editable = true;

		_lineBreaks.Add( BufferSize );

		_scrollBarSize = IsProportional ? (int)((float)vgui.Surface.ScreenWide / ProportionalBaseWide * ScrollBarSize) : ScrollBarSize;
	}

	public string Text => new( _textStream.ToArray() );

	public bool IsEditable => _editable && Enabled;

	public int GetMaximumCharCount() => _maxCharCount;

	public void SetMaximumCharCount( int maxChars ) => _maxCharCount = maxChars;

	public void SetEditable( bool state ) => _editable = state;

	public void SetMultiline( bool state ) => _multiline = state;

	public void SetCatchEnterKey( bool state ) => _catchEnterKey = state;

	public void SendNewLine( bool send ) => _sendNewLines = send;

	public void SetWrap( bool wrap ) => _wrap = wrap;

	public void SetAutoProgressOnHittingCharLimit( bool state ) => _autoProgressOnHittingCharLimit = state;

	public void SetHorizontalScrolling( bool status ) => _horizScrollingAllowed = status;

	public void SetDisabledBgColor( Color32 color ) => _disabledBgColor = color;

	public void SetSelectAllOnFirstFocus( bool state ) => _selectAllOnFirstFocus = state;

	public void SetUseFallbackFont( bool state, SchemeFont fallback )
	{
		_useFallbackFont = state;
		_fallbackFont = fallback;
	}

	protected void SetDrawWidth( int width ) => _drawWidth = width;

	public void SetTextHidden( bool hideText ) => _hideText = hideText;

	public void SetFont( SchemeFont font )
	{
		_font = font;

		InvalidateLayout();
	}

	public void SetVerticalScrollbar( bool state )
	{
		_verticalScrollbar = state;

		if ( state )
		{
			if ( _vertScrollBar is null )
			{
				_vertScrollBar = new ScrollBar( Vgui, this, "ScrollBar", true );
				_vertScrollBar.AddActionSignalTarget( this );
			}

			_vertScrollBar.SetVisible( true );
		}
		else
		{
			_vertScrollBar?.SetVisible( false );
		}

		InvalidateLayout();
	}

	public virtual void SetText( string text )
	{
		text = Vgui.Localize( text );

		_textStream.Clear();

		var missed = 0;

		for ( var i = 0; i < text.Length; i++ )
		{
			if ( text[i] == '\r' )
			{
				missed++;
				continue;
			}

			_textStream.Add( text[i] );

			SetCharAt( text[i], i - missed );
		}

		GotoTextStart();
		SelectNone();

		_dataChanged = false;

		ResetLineBreaks();
		InvalidateLayout();
	}

	public void SetState( int state ) => SetText( state.ToString( CultureInfo.InvariantCulture ) );

	private void ResetLineBreaks()
	{
		_recalculateBreaksIndex = 0;

		_lineBreaks.Clear();
		_lineBreaks.Add( BufferSize );
	}

	private void SetCharAt( char character, int index )
	{
		if ( character is '\n' or '\0' )
			ResetLineBreaks();

		if ( index < 0 )
			return;

		while ( _textStream.Count <= index )
			_textStream.Add( '\0' );

		_textStream[index] = character;
		_dataChanged = true;
	}

	private static bool IsPrint( char character )
	{
		return character == '\t' || char.GetUnicodeCategory( character ) is not (UnicodeCategory.Control or UnicodeCategory.OtherNotAssigned);
	}

	private static int CharWidth( SchemeFont font, char character ) => IsPrint( character ) ? Surface.GetCharacterWidth( font, character ) : 0;

	private char DisplayChar( int index ) => _hideText ? '*' : _textStream[index];

	private void ResetCursorBlink()
	{
		_cursorBlink = false;
		_cursorNextBlinkTime = Vgui.TimeMillis + CursorBlinkRate;
	}

	private void SelectCheck()
	{
		if ( !HasFocus || !Vgui.Input.IsShiftDown )
			_selectStart = -1;
		else if ( _selectStart == -1 )
			_selectStart = _cursorPos;
	}

	public void SelectAllText()
	{
		_selectStart = _textStream.Count != 0 ? 0 : -1;
		_selectEnd = _textStream.Count;
	}

	public void SelectNone() => _selectStart = -1;

	protected void SelectNoText()
	{
		_selectStart = -1;
		_selectEnd = 0;
	}

	private bool GetSelectedRange( out int start, out int end )
	{
		if ( _selectStart == -1 )
		{
			start = -1;
			end = -1;

			return false;
		}

		start = Math.Min( _selectStart, _selectEnd );
		end = Math.Max( _selectStart, _selectEnd );

		return true;
	}

	private bool IsLineBreak( int index ) => _lineBreaks.Contains( index );

	private int GetCursorLine()
	{
		int cursorLine;

		for ( cursorLine = 0; cursorLine < _lineBreaks.Count; cursorLine++ )
		{
			if ( _cursorPos < _lineBreaks[cursorLine] )
				break;
		}

		if ( _putCursorAtEnd && _cursorPos != _textStream.Count )
			cursorLine--;

		return cursorLine;
	}

	private int GetCurrentLineStart()
	{
		if ( !_multiline )
			return _currentStartIndex;

		int i;

		if ( IsLineBreak( _cursorPos ) )
		{
			for ( i = 0; i < _lineBreaks.Count; i++ )
			{
				if ( _cursorPos == _lineBreaks[i] )
					break;
			}

			if ( !_cursorIsAtEnd )
				return _cursorPos;

			return i > 0 ? _lineBreaks[i - 1] : _lineBreaks[0];
		}

		for ( i = 0; i < _lineBreaks.Count; i++ )
		{
			if ( _cursorPos < _lineBreaks[i] )
				return i == 0 ? 0 : _lineBreaks[i - 1];
		}

		return 0;
	}

	private int GetCurrentLineEnd()
	{
		int i;

		if ( IsLineBreak( _cursorPos ) )
		{
			for ( i = 0; i < _lineBreaks.Count - 1; i++ )
			{
				if ( _cursorPos == _lineBreaks[i] )
					break;
			}

			if ( _cursorIsAtEnd )
				return _cursorPos;

			if ( i != _lineBreaks.Count - 2 )
				return _lineBreaks[i + 1];
		}

		for ( i = 0; i < _lineBreaks.Count - 1; i++ )
		{
			if ( _cursorPos < _lineBreaks[i] )
				return _lineBreaks[i];
		}

		return _textStream.Count;
	}

	private void AddAnotherLine( ref int x, ref int y )
	{
		x = DrawOffsetX + _pixelsIndent;
		y += Surface.GetFontTall( _font ) + DrawOffsetY;
	}

	private int GetYStart() => _multiline ? DrawOffsetY : (Tall / 2) - (Surface.GetFontTall( _font ) / 2);

	private void MoveScrollBar( int delta ) => _vertScrollBar?.SetValue( _vertScrollBar.Value - (delta * MouseWheelLines) );

	private int GetStartDrawIndex( ref int lineBreakIndexIndex )
	{
		var startIndex = 0;
		var numLines = _lineBreaks.Count;
		var startLine = 0;

		if ( _vertScrollBar is not null && !_mouseDragSelection )
		{
			startLine = _vertScrollBar.Value;
		}
		else
		{
			var displayLines = Tall / (Surface.GetFontTall( _font ) + DrawOffsetY);

			if ( displayLines < numLines )
			{
				var cursorLine = GetCursorLine();

				startLine = _currentStartLine;

				if ( cursorLine < _currentStartLine )
				{
					startLine = cursorLine;

					if ( _vertScrollBar is not null )
					{
						MoveScrollBar( 1 );

						startLine = _vertScrollBar.Value;
					}
				}
				else if ( cursorLine > _currentStartLine + displayLines - 1 )
				{
					startLine = cursorLine - displayLines + 1;

					if ( _vertScrollBar is not null )
					{
						MoveScrollBar( -1 );

						startLine = _vertScrollBar.Value;
					}
				}
			}
			else if ( !_multiline )
			{
				var x = DrawOffsetX;

				for ( var i = _currentStartIndex; i < _textStream.Count; i++ )
				{
					if ( _cursorPos == i )
						break;

					x += CharWidth( _font, DisplayChar( i ) );
				}

				if ( x >= Wide )
					_currentStartIndex++;
				else if ( x <= 0 && _currentStartIndex > 0 )
					_currentStartIndex--;
			}
		}

		if ( startLine > 0 )
		{
			lineBreakIndexIndex = startLine;

			if ( startLine < _lineBreaks.Count )
				startIndex = _lineBreaks[startLine - 1];
		}

		if ( !_horizScrollingAllowed )
			return 0;

		_currentStartLine = startLine;

		return _multiline ? startIndex : _currentStartIndex;
	}

	private void CursorToPixelSpace( int cursorPos, out int cx, out int cy )
	{
		var x = DrawOffsetX;
		var y = GetYStart();

		_pixelsIndent = 0;

		var lineBreakIndexIndex = 0;

		for ( var i = GetStartDrawIndex( ref lineBreakIndexIndex ); i < _textStream.Count; i++ )
		{
			if ( cursorPos == i )
				break;

			if ( lineBreakIndexIndex < _lineBreaks.Count && _lineBreaks[lineBreakIndexIndex] == i )
			{
				AddAnotherLine( ref x, ref y );

				lineBreakIndexIndex++;
			}

			x += CharWidth( _font, DisplayChar( i ) );
		}

		cx = x;
		cy = y;
	}

	private int PixelToCursorSpace( int cx, int cy )
	{
		_putCursorAtEnd = false;

		var fontTall = Surface.GetFontTall( _font );
		var yStart = GetYStart();
		var x = DrawOffsetX;
		var y = yStart;

		_pixelsIndent = 0;

		var lineBreakIndexIndex = 0;
		var onRightLine = false;
		int i;

		for ( i = GetStartDrawIndex( ref lineBreakIndexIndex ); i < _textStream.Count; i++ )
		{
			if ( lineBreakIndexIndex < _lineBreaks.Count && _lineBreaks[lineBreakIndexIndex] == i )
			{
				AddAnotherLine( ref x, ref y );

				lineBreakIndexIndex++;

				if ( onRightLine )
				{
					_putCursorAtEnd = true;

					return i;
				}
			}

			if ( cy < yStart )
			{
				onRightLine = true;
				_putCursorAtEnd = true;
			}
			else if ( cy >= y && cy < y + fontTall + DrawOffsetY )
			{
				onRightLine = true;
			}

			var wide = CharWidth( _font, DisplayChar( i ) );

			if ( onRightLine )
			{
				if ( cx <= Wide && (cx < DrawOffsetX + _pixelsIndent || cy < yStart) )
					return i;

				if ( cx >= x && cx < x + wide )
					return cx < x + (wide * 0.5) ? i : i + 1;
			}

			x += wide;
		}

		return i;
	}

	private void ScrollRight()
	{
		if ( !_horizScrollingAllowed )
			return;

		if ( !_multiline )
		{
			CursorToPixelSpace( _cursorPos, out var cx, out _ );

			if ( Wide - 1 >= 0 && cx >= Wide - 1 )
			{
				_currentStartIndex++;

				ScrollRight();
			}
		}

		LayoutVerticalScrollBarSlider();
	}

	private void ScrollLeft()
	{
		if ( _multiline || !_horizScrollingAllowed )
			return;

		if ( _cursorPos < _currentStartIndex )
		{
			if ( _cursorPos < 0 )
				_cursorPos = 0;

			_currentStartIndex = _cursorPos;
		}

		LayoutVerticalScrollBarSlider();
	}

	public void GotoLeft()
	{
		SelectCheck();

		if ( IsLineBreak( _cursorPos ) && !_cursorIsAtEnd )
			_putCursorAtEnd = true;

		if ( !_putCursorAtEnd && _cursorPos > 0 )
			_cursorPos--;

		ScrollLeft();
		ResetCursorBlink();
	}

	public void GotoRight()
	{
		SelectCheck();

		if ( IsLineBreak( _cursorPos ) )
		{
			if ( _cursorIsAtEnd )
				_putCursorAtEnd = false;
			else if ( _cursorPos < _textStream.Count )
				_cursorPos++;
		}
		else
		{
			if ( _cursorPos < _textStream.Count )
				_cursorPos++;

			if ( IsLineBreak( _cursorPos ) && !_cursorIsAtEnd )
				_putCursorAtEnd = true;
		}

		ScrollRight();
		ResetCursorBlink();
	}

	public void GotoUp()
	{
		SelectCheck();

		if ( _cursorIsAtEnd )
		{
			if ( GetCursorLine() == 1 )
			{
				_putCursorAtEnd = true;
				return;
			}

			_cursorPos--;
		}

		CursorToPixelSpace( _cursorPos, out var cx, out _ );

		MoveCursor( GetCursorLine() - 1, cx );
	}

	public void GotoDown()
	{
		SelectCheck();

		if ( _cursorIsAtEnd )
		{
			_cursorPos--;

			if ( _cursorPos < 0 )
				_cursorPos = 0;
		}

		CursorToPixelSpace( _cursorPos, out var cx, out _ );

		MoveCursor( GetCursorLine() + 1, cx );

		if ( !_putCursorAtEnd && _cursorIsAtEnd )
		{
			_cursorPos++;

			if ( _cursorPos > _textStream.Count )
				_cursorPos = _textStream.Count;
		}

		LayoutVerticalScrollBarSlider();
	}

	public void GotoWordRight()
	{
		SelectCheck();

		while ( ++_cursorPos < _textStream.Count )
		{
			if ( char.IsWhiteSpace( _textStream[_cursorPos] ) )
				break;
		}

		while ( ++_cursorPos < _textStream.Count )
		{
			if ( !char.IsWhiteSpace( _textStream[_cursorPos] ) )
				break;
		}

		if ( _cursorPos > _textStream.Count )
			_cursorPos = _textStream.Count;

		ScrollRight();
		LayoutVerticalScrollBarSlider();
		ResetCursorBlink();
	}

	public void GotoWordLeft()
	{
		SelectCheck();

		if ( _cursorPos < 1 )
			return;

		while ( --_cursorPos >= 0 )
		{
			if ( !char.IsWhiteSpace( _textStream[_cursorPos] ) )
				break;
		}

		while ( --_cursorPos >= 0 )
		{
			if ( char.IsWhiteSpace( _textStream[_cursorPos] ) )
				break;
		}

		_cursorPos++;

		ScrollLeft();
		LayoutVerticalScrollBarSlider();
		ResetCursorBlink();
	}

	public void GotoFirstOfLine()
	{
		SelectCheck();

		_cursorPos = GetCurrentLineStart();
		_currentStartIndex = _cursorPos;
		_putCursorAtEnd = false;

		LayoutVerticalScrollBarSlider();
		ResetCursorBlink();
	}

	public void GotoEndOfLine()
	{
		SelectCheck();

		_cursorPos = GetCurrentLineEnd();
		_putCursorAtEnd = true;

		ScrollRight();
		LayoutVerticalScrollBarSlider();
		ResetCursorBlink();
	}

	public void GotoTextStart()
	{
		SelectCheck();

		_cursorPos = 0;
		_putCursorAtEnd = false;
		_currentStartIndex = 0;

		LayoutVerticalScrollBarSlider();
		ResetCursorBlink();
	}

	public void GotoTextEnd()
	{
		SelectCheck();

		_cursorPos = _textStream.Count;
		_putCursorAtEnd = true;

		ScrollRight();
		LayoutVerticalScrollBarSlider();
		ResetCursorBlink();
	}

	private void MoveCursor( int line, int pixelsAcross )
	{
		if ( line < 0 )
			line = 0;

		if ( line >= _lineBreaks.Count )
			line = _lineBreaks.Count - 1;

		var x = DrawOffsetX;
		var y = GetYStart();
		var lineBreakIndexIndex = 0;

		_pixelsIndent = 0;

		int i;

		for ( i = 0; i < _textStream.Count; i++ )
		{
			if ( _lineBreaks[lineBreakIndexIndex] == i )
			{
				if ( lineBreakIndexIndex == line )
				{
					_putCursorAtEnd = true;
					_cursorPos = i;
					break;
				}

				AddAnotherLine( ref x, ref y );

				lineBreakIndexIndex++;
			}

			var charWidth = CharWidth( _font, DisplayChar( i ) );

			if ( line == lineBreakIndexIndex && x + (charWidth / 2) > pixelsAcross )
			{
				_cursorPos = i;
				break;
			}

			x += charWidth;
		}

		if ( i == _textStream.Count )
			GotoTextEnd();

		LayoutVerticalScrollBarSlider();
		ResetCursorBlink();
	}

	private void CalcBreakIndex()
	{
		if ( _cursorPos == _textStream.Count )
		{
			_recalculateBreaksIndex = _lineBreaks.Count - 2;
			return;
		}

		_recalculateBreaksIndex = 0;

		while ( _cursorPos > _lineBreaks[_recalculateBreaksIndex] )
			_recalculateBreaksIndex++;

		_recalculateBreaksIndex--;
	}

	public void InsertChar( char character )
	{
		if ( character == '\r' )
			return;

		if ( !_multiline && character == '\n' )
			return;

		if ( character == '\t' )
			return;

		if ( _allowNumericInputOnly && !char.IsDigit( character ) && character != '.' )
		{
			Surface.PlaySound( WarningSound );
			return;
		}

		if ( !_allowNonAsciiCharacters && character > 127 )
			return;

		if ( _maxCharCount > -1 && _textStream.Count >= _maxCharCount )
		{
			if ( _maxCharCount < 1 || !_multiline || !_wrap )
			{
				Surface.PlaySound( WarningSound );
				return;
			}

			while ( _textStream.Count > _maxCharCount )
			{
				if ( _recalculateBreaksIndex == 0 )
					RecalculateLineBreaks();

				if ( _lineBreaks[0] > _textStream.Count )
				{
					_recalculateBreaksIndex = -1;

					RecalculateLineBreaks();
				}

				if ( _lineBreaks[0] + 1 >= _textStream.Count )
					continue;

				var removed = _lineBreaks[0];

				_textStream.RemoveRange( 0, removed );

				if ( _cursorPos > _textStream.Count )
					_cursorPos = _textStream.Count;
				else
					_cursorPos = Math.Max( _cursorPos - removed - 1, 0 );

				if ( _selectStart > -1 )
				{
					_selectStart -= removed + 1;

					if ( _selectStart <= 0 )
						_selectStart = -1;

					_selectEnd -= removed + 1;

					if ( _selectEnd <= 0 )
						_selectEnd = -1;
				}

				for ( var i = _textStream.Count - 1; i >= 0; i-- )
					SetCharAt( _textStream[i], i + 1 );

				_recalculateBreaksIndex = -1;

				RecalculateLineBreaks();
			}
		}

		if ( _wrap )
		{
			SetCharAt( character, _textStream.Count );

			_cursorPos = _textStream.Count;
		}
		else
		{
			for ( var i = _textStream.Count - 1; i >= _cursorPos; i-- )
				SetCharAt( _textStream[i], i + 1 );

			SetCharAt( character, _cursorPos );

			_cursorPos++;
		}

		if ( character == '\n' )
			RecalculateLineBreaks();

		if ( _autoProgressOnHittingCharLimit && _textStream.Count == _maxCharCount )
			RequestFocusNext();

		ScrollRight();

		_dataChanged = true;

		CalcBreakIndex();
		LayoutVerticalScrollBarSlider();
		ResetCursorBlink();
	}

	public void InsertString( string text )
	{
		text = Vgui.Localize( text );

		SaveUndoState();

		foreach ( var character in text )
			InsertChar( character );

		if ( _dataChanged )
			FireActionSignal();
	}

	public void Backspace()
	{
		if ( !IsEditable || _cursorPos == 0 || _textStream.Count == 0 )
			return;

		SaveUndoState();

		for ( var i = _cursorPos; i < _textStream.Count; i++ )
			SetCharAt( _textStream[i], i - 1 );

		_textStream.RemoveAt( _textStream.Count - 1 );

		if ( _cursorPos == _currentStartIndex )
			_currentStartIndex = Math.Max( _currentStartIndex - BackspaceScroll, 0 );

		_dataChanged = true;
		_cursorPos--;

		CalcBreakIndex();
		LayoutVerticalScrollBarSlider();
		ResetCursorBlink();
	}

	public void DeleteChar()
	{
		if ( !IsEditable || _textStream.Count == 0 )
			return;

		if ( !GetSelectedRange( out var start, out var end ) )
		{
			start = _cursorPos;
			end = start + 1;

			if ( _cursorPos >= _textStream.Count )
				return;
		}

		SaveUndoState();

		if ( end > start )
			_textStream.RemoveRange( start, end - start );

		ResetCursorBlink();
		SelectNone();

		_cursorPos = start;
		_dataChanged = true;

		ResetLineBreaks();
		CalcBreakIndex();
		LayoutVerticalScrollBarSlider();
	}

	public void DeleteSelected()
	{
		if ( !IsEditable || _textStream.Count == 0 )
			return;

		if ( !GetSelectedRange( out var start, out var end ) )
			return;

		SaveUndoState();

		if ( end > start )
			_textStream.RemoveRange( start, end - start );

		SelectNone();
		ResetCursorBlink();

		_cursorPos = start;
		_dataChanged = true;

		ResetLineBreaks();
		CalcBreakIndex();
		LayoutVerticalScrollBarSlider();
	}

	public void Undo()
	{
		_cursorPos = _undoCursorPos;

		_textStream.Clear();
		_textStream.AddRange( _undoTextStream );

		InvalidateLayout();
		SelectNone();
	}

	public void SaveUndoState()
	{
		_undoCursorPos = _cursorPos;

		_undoTextStream.Clear();
		_undoTextStream.AddRange( _textStream );
	}

	public void CutSelected()
	{
		CopySelected();
		DeleteSelected();
		RequestFocus();

		if ( _dataChanged )
			FireActionSignal();
	}

	public void CopySelected()
	{
		if ( _hideText )
			return;

		if ( GetSelectedRange( out var start, out var end ) )
		{
			var buffer = new List<char>();

			for ( var i = start; i < end; i++ )
			{
				if ( _textStream[i] == '\n' )
					buffer.Add( '\r' );

				buffer.Add( _textStream[i] );
			}

			Vgui.SetClipboardText( new string( buffer.ToArray(), 0, end - start ) );
		}

		RequestFocus();

		if ( _dataChanged )
			FireActionSignal();
	}

	public void Paste()
	{
		if ( _hideText || !IsEditable )
			return;

		var clipboard = Vgui.ClipboardText;
		var bufferSize = clipboard.Length + 1;

		if ( !_autoProgressOnHittingCharLimit && _maxCharCount > 0 )
			bufferSize = _maxCharCount + 1;

		var length = Math.Min( clipboard.Length, bufferSize - 1 );

		if ( length < 1 )
			return;

		SaveUndoState();

		var movedFocusAway = false;

		for ( var i = 0; i < length && clipboard[i] != '\0'; i++ )
		{
			if ( _autoProgressOnHittingCharLimit && _textStream.Count == _maxCharCount )
			{
				RequestFocusNext();

				Vgui.SetClipboardText( clipboard.Substring( i, Math.Max( length - i - 1, 0 ) ) );

				if ( Parent?.GetCurrentKeyFocus() is { } focus && focus != this )
				{
					movedFocusAway = true;

					focus.OnMessage( new Message( "DoPaste" ), this );
				}

				break;
			}

			InsertChar( clipboard[i] );
		}

		if ( _autoProgressOnHittingCharLimit )
			Vgui.SetClipboardText( clipboard );

		_dataChanged = true;

		FireActionSignal();

		if ( !movedFocusAway )
			RequestFocus();
	}

	protected virtual void FireActionSignal()
	{
		PostActionSignal( new Message( "TextChanged" ) );

		_dataChanged = false;

		InvalidateLayout();
	}

	private void CreateEditMenu()
	{
		_editMenu?.Delete();
		_editMenu = new Menu( Vgui, this, "EditMenu" );

		if ( _editable && !_hideText )
			_editMenu.AddMenuItem( "#TextEntry_Cut", "#TextEntry_Cut", new Message( "DoCutSelected" ), this );

		if ( !_hideText )
			_editMenu.AddMenuItem( "#TextEntry_Copy", "#TextEntry_Copy", new Message( "DoCopySelected" ), this );

		if ( _editable )
			_editMenu.AddMenuItem( "#TextEntry_Paste", "#TextEntry_Paste", new Message( "DoPaste" ), this );

		_editMenu.SetVisible( false );
		_editMenu.SetParent( this );
		_editMenu.AddActionSignalTarget( this );
	}

	public void OpenEditMenu()
	{
		var (cursorX, cursorY) = Vgui.Input.CursorPos;
		var selected = GetSelectedRange( out _, out _ );

		_editMenu.SetItemEnabled( "&Cut", selected );
		_editMenu.SetItemEnabled( "C&opy", selected );

		_editMenu.SetVisible( true );
		_editMenu.RequestFocus();
		_editMenu.InvalidateLayout( true );

		var menuWide = _editMenu.Wide;
		var menuTall = _editMenu.Tall;
		var surface = Vgui.Surface;

		if ( surface.ScreenTall - menuTall <= cursorY )
			cursorY -= menuTall;

		if ( surface.ScreenWide - menuWide <= cursorX )
			cursorX -= menuWide;

		_editMenu.SetPos( cursorX, cursorY );
		_editMenu.RequestFocus();
	}

	public void RecalculateLineBreaks()
	{
		if ( !_multiline || _hideText || _textStream.Count < 1 )
			return;

		var wide = Wide - 2;

		if ( _vertScrollBar is not null )
			wide -= _vertScrollBar.Wide;

		var x = DrawOffsetX;
		var y = DrawOffsetY;
		var wordStartIndex = 0;
		var hasWord = false;
		var justStartedNewLine = true;
		var wordStartedOnNewLine = true;
		int startChar;

		if ( _recalculateBreaksIndex <= 0 )
		{
			_lineBreaks.Clear();

			startChar = 0;
		}
		else
		{
			if ( _recalculateBreaksIndex + 1 < _lineBreaks.Count )
				_lineBreaks.RemoveRange( _recalculateBreaksIndex + 1, _lineBreaks.Count - _recalculateBreaksIndex - 1 );

			startChar = _lineBreaks[_recalculateBreaksIndex];
		}

		if ( startChar < _textStream.Count && _textStream[startChar] is '\r' or '\n' )
			startChar++;

		int i;

		for ( i = startChar; i < _textStream.Count; i++ )
		{
			var character = _textStream[i];

			if ( char.IsWhiteSpace( character ) )
			{
				hasWord = false;
			}
			else if ( !hasWord )
			{
				wordStartIndex = i;
				hasWord = true;
				wordStartedOnNewLine = justStartedNewLine;
			}

			var charWidth = CharWidth( _font, character );

			if ( IsPrint( character ) )
				justStartedNewLine = false;

			if ( x + charWidth >= wide || character is '\r' or '\n' )
			{
				AddAnotherLine( ref x, ref y );

				justStartedNewLine = true;
				hasWord = false;

				if ( character is '\r' or '\n' || wordStartedOnNewLine || char.IsWhiteSpace( character ) )
				{
					_lineBreaks.Add( i );
				}
				else
				{
					_lineBreaks.Add( wordStartIndex );

					i = wordStartIndex;
				}
			}

			x += charWidth;
		}

		_lineBreaks.Add( BufferSize );

		LayoutVerticalScrollBarSlider();
	}

	private void LayoutVerticalScrollBarSlider()
	{
		if ( _vertScrollBar is null )
			return;

		_vertScrollBar.SetPos( Wide - InsetTop - _scrollBarSize, 0 );
		_vertScrollBar.SetSize( _scrollBarSize, Tall - InsetRight - InsetBottom );

		var displayLines = Tall / (Surface.GetFontTall( _font ) + DrawOffsetY);
		var numLines = _lineBreaks.Count;

		if ( numLines <= displayLines )
		{
			_vertScrollBar.SetEnabled( false );
			_vertScrollBar.SetRange( 0, numLines );
			_vertScrollBar.SetRangeWindow( numLines );
			_vertScrollBar.SetValue( 0 );
			return;
		}

		_vertScrollBar.SetRange( 0, numLines );
		_vertScrollBar.SetRangeWindow( displayLines );
		_vertScrollBar.SetEnabled( true );
		_vertScrollBar.SetButtonPressedScrollValue( 1 );

		var value = _vertScrollBar.Value;
		var maxValue = value + displayLines;
		var cursorLine = GetCursorLine();

		if ( cursorLine < value )
			value = cursorLine;
		else if ( cursorLine >= maxValue )
			value = cursorLine + 1 - displayLines;

		_vertScrollBar.SetValue( value );
		_vertScrollBar.InvalidateLayout();
	}

	private bool NeedsEllipses( SchemeFont font, out int index )
	{
		var wide = DrawOffsetX;

		for ( index = 0; index < _textStream.Count; index++ )
		{
			wide += CharWidth( font, _textStream[index] );

			if ( wide > _drawWidth )
				return true;
		}

		index = -1;

		return false;
	}

	private int DrawChar( char character, SchemeFont font, int index, int x, int y )
	{
		if ( !IsPrint( character ) )
			return 0;

		var surface = Vgui.Surface;
		var charWide = Surface.GetCharacterWidth( font, character );

		GetSelectedRange( out var start, out var end );

		if ( index >= start && index < end )
		{
			var focused = HasFocus || (Vgui.Input.Focus is { } focus && focus.HasParent( this ));

			surface.DrawSetColor( focused ? _selectionColor : _defaultSelectionBG2Color );
			surface.DrawFilledRect( x, y, x + charWide, y + Surface.GetFontTall( font ) + 1 );
			surface.DrawSetTextColor( _selectionTextColor );
		}

		surface.DrawSetTextPos( x, y );
		surface.DrawUnicodeChar( character );

		return charWide;
	}

	private void DrawCursor()
	{
		if ( _cursorBlink )
			return;

		CursorToPixelSpace( _cursorPos, out var cx, out var cy );

		var surface = Vgui.Surface;

		surface.DrawSetColor( _cursorColor );
		surface.DrawFilledRect( cx, cy, cx + 1, cy + Surface.GetFontTall( _font ) );
	}

	public override void PaintBackground()
	{
		var surface = Vgui.Surface;

		surface.DrawSetColor( Enabled ? GetBgColor() : _disabledBgColor );
		surface.DrawFilledRect( 0, 0, Wide, Tall );

		var font = _font;

		surface.DrawSetTextFont( font );
		surface.DrawSetTextColor( Enabled ? GetFgColor() : _disabledFgColor );

		_pixelsIndent = 0;

		var lineBreakIndexIndex = 0;
		var i = GetStartDrawIndex( ref lineBreakIndexIndex );
		var x = DrawOffsetX + _pixelsIndent;
		var y = GetYStart();

		if ( _multiline || _horizScrollingAllowed )
		{
			for ( ; i < _textStream.Count; i++ )
			{
				if ( _multiline && lineBreakIndexIndex < _lineBreaks.Count && _lineBreaks[lineBreakIndexIndex] == i )
				{
					AddAnotherLine( ref x, ref y );

					lineBreakIndexIndex++;
				}

				x += DrawChar( DisplayChar( i ), font, i, x, y );
			}
		}
		else
		{
			var endIndex = _textStream.Count;

			if ( !HasFocus || !IsEditable )
			{
				var needsEllipses = NeedsEllipses( font, out var index );

				if ( needsEllipses && !IsEditable && _useFallbackFont && _fallbackFont is not null )
				{
					font = _fallbackFont;

					surface.DrawSetTextFont( font );

					needsEllipses = NeedsEllipses( font, out index );
				}

				if ( needsEllipses )
				{
					var ellipsesWidth = CharWidth( font, '.' ) * EllipsisDots;

					for ( ; ellipsesWidth > 0 && index >= 0; index-- )
						ellipsesWidth -= CharWidth( font, _textStream[index] );

					endIndex = index + 1;
				}

				if ( (uint)(_textStream.Count - endIndex - 1) < 2 )
					endIndex = _textStream.Count - EllipsisDots;
			}

			for ( ; i < endIndex; i++ )
				x += DrawChar( DisplayChar( i ), font, i, x, y );

			if ( endIndex < _textStream.Count )
			{
				for ( var dot = 0; dot < EllipsisDots; dot++ )
					x += DrawChar( '.', font, i + dot, x, y );
			}
		}

		if ( Enabled && IsEditable && HasFocus )
			DrawCursor();
	}

	public override void PerformLayout()
	{
		base.PerformLayout();

		RecalculateLineBreaks();

		if ( _verticalScrollbar )
			LayoutVerticalScrollBarSlider();
	}

	public override void OnSizeChanged( int wide, int tall )
	{
		base.OnSizeChanged( wide, tall );

		ResetLineBreaks();

		if ( _drawWidth < wide && !_multiline && _horizScrollingAllowed )
		{
			while ( _currentStartIndex > 0 )
			{
				var previous = --_currentStartIndex;

				CursorToPixelSpace( _cursorPos, out var cx, out _ );

				if ( Wide - 1 >= 0 && cx >= Wide - 1 )
				{
					_currentStartIndex++;
					break;
				}

				if ( previous != _currentStartIndex )
					break;
			}

			LayoutVerticalScrollBarSlider();
		}

		_drawWidth = wide;

		InvalidateLayout();
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetFgColor( GetSchemeColor( "WindowFgColor" ) );
		SetBgColor( GetSchemeColor( "WindowBgColor" ) );

		_cursorColor = GetSchemeColor( "TextCursorColor" );
		_disabledFgColor = GetSchemeColor( "WindowDisabledFgColor" );
		_disabledBgColor = GetSchemeColor( "ControlBG" );
		_selectionTextColor = GetSchemeColor( "SelectionFgColor", GetFgColor() );
		_selectionColor = GetSchemeColor( "SelectionBgColor" );
		_defaultSelectionBG2Color = GetSchemeColor( "SelectionBG2" );

		SetBorder( Vgui.Scheme.GetBorder( "ButtonDepressedBorder" ) );
		SetFont( GetFont( "Default" ) );
	}

	public override void ApplySettings( KeyValues settings )
	{
		base.ApplySettings( settings );

		SetTextHidden( settings.GetInt( "textHidden" ) != 0 );
		SetEditable( settings.GetInt( "editable", 1 ) != 0 );
		SetMaximumCharCount( settings.GetInt( "maxchars", -1 ) );

		_allowNumericInputOnly = settings.GetInt( "NumericInputOnly" ) != 0;
		_allowNonAsciiCharacters = settings.GetInt( "unicode" ) != 0;
	}

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "DoCutSelected":
				CutSelected();
				break;

			case "DoCopySelected":
				CopySelected();
				break;

			case "DoPaste":
				Paste();
				break;

			case "SetText":
				SetText( message.Text );
				break;

			case "SetState":
				SetState( message.Int );
				break;

			case "ScrollBarSliderMoved":
				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}

	public override void OnMouseCaptureLost() => _mouseSelection = false;

	public override void OnSetFocus()
	{
		if ( _selectAllOnFirstFocus )
		{
			_selectAllOnFirstFocus = false;
			_selectStart = 0;
			_selectEnd = _textStream.Count;
			_cursorPos = _textStream.Count;
			return;
		}

		var input = Vgui.Input;

		if ( !input.IsMouseDown( MouseCode.Left ) && !input.IsMouseDown( MouseCode.Right ) )
		{
			GotoTextEnd();
			SelectNone();
		}
	}

	public override void OnKillFocus()
	{
		if ( _dataChanged )
		{
			FireActionSignal();

			_dataChanged = false;
		}

		var input = Vgui.Input;

		if ( input.WasMousePressed( MouseCode.Right ) || input.IsMouseDown( MouseCode.Right ) || input.WasMouseReleased( MouseCode.Right ) )
		{
			var (cursorX, cursorY) = input.CursorPos;

			if ( IsWithin( cursorX, cursorY ) )
				return;
		}

		if ( IsEditable )
			SelectNone();
	}

	public override void OnCursorMoved( int x, int y )
	{
		if ( !_mouseSelection )
			return;

		var (cursorX, cursorY) = Vgui.Input.CursorPos;

		(x, y) = ScreenToLocal( cursorX, cursorY );

		_cursorPos = PixelToCursorSpace( x, y );

		if ( _cursorPos == 0 )
			_putCursorAtEnd = false;

		if ( _cursorPos == _currentStartIndex )
		{
			if ( _cursorPos > 0 )
				_cursorPos--;

			ScrollLeft();

			_cursorPos = _currentStartIndex;
		}

		if ( _cursorPos != _selectEnd )
			_selectEnd = _cursorPos;
	}

	public override void OnCursorEntered() => _mouseDragSelection = false;

	public override void OnCursorExited()
	{
		if ( _mouseSelection )
			_mouseDragSelection = true;
	}

	public override void OnMousePressed( MouseCode code )
	{
		if ( code == MouseCode.Left )
		{
			SelectCheck();

			var (cursorX, cursorY) = Vgui.Input.CursorPos;
			var (x, y) = ScreenToLocal( cursorX, cursorY );

			_cursorIsAtEnd = _putCursorAtEnd;
			_cursorPos = PixelToCursorSpace( x, y );

			if ( _cursorPos == 0 )
				_putCursorAtEnd = false;

			Vgui.Input.SetMouseCapture( this );

			_mouseSelection = true;

			if ( _selectStart < 0 )
				_selectStart = _cursorPos;

			_selectEnd = _cursorPos;

			ResetCursorBlink();
			RequestFocus();
		}
		else if ( code == MouseCode.Right )
		{
			CreateEditMenu();
			OpenEditMenu();
		}
	}

	public override void OnMouseDoublePressed( MouseCode code )
	{
		if ( code != MouseCode.Left )
			return;

		OnMousePressed( MouseCode.Left );
		GotoWordLeft();

		var start = _cursorPos;

		GotoWordRight();

		var end = _cursorPos;

		if ( _cursorPos <= 0 )
			return;

		if ( char.IsWhiteSpace( _textStream[end - 1] ) )
		{
			end--;
			_cursorPos--;
		}

		_selectStart = start;
		_selectEnd = end;
		_mouseSelection = true;
	}

	public override void OnMouseReleased( MouseCode code )
	{
		_mouseSelection = false;

		Vgui.Input.SetMouseCapture( null );

		if ( GetSelectedRange( out var start, out var end ) && end == start )
			_selectStart = -1;
	}

	public override void OnMouseWheeled( int delta )
	{
		if ( _vertScrollBar is not null )
			MoveScrollBar( delta );
		else
			base.OnMouseWheeled( delta );
	}

	private void InsertNewLine()
	{
		DeleteSelected();
		SaveUndoState();
		InsertChar( '\n' );
	}

	private void PageScroll( int direction )
	{
		var value = _vertScrollBar?.Value ?? 0;

		if ( _multiline )
		{
			var displayLines = Tall / (Surface.GetFontTall( _font ) + DrawOffsetY);

			for ( var i = 0; i < displayLines; i++ )
			{
				if ( direction < 0 )
					GotoUp();
				else
					GotoDown();
			}
		}

		if ( _vertScrollBar is null )
			return;

		var linesToMove = _vertScrollBar.RangeWindow - (direction * (_vertScrollBar.Value - value));

		_vertScrollBar.SetValue( value + (direction * (linesToMove + 1)) );
	}

	public override void OnKeyCodeTyped( KeyCode code )
	{
		_cursorIsAtEnd = _putCursorAtEnd;
		_putCursorAtEnd = false;

		var input = Vgui.Input;
		var shift = input.IsShiftDown;
		var fallThrough = false;
		var selectEnd = -1;

		if ( input.IsControlDown )
		{
			switch ( code )
			{
				case KeyCode.A:
					SelectAllText();

					selectEnd = _textStream.Count;
					break;

				case KeyCode.C:
				case KeyCode.Insert:
					CopySelected();
					break;

				case KeyCode.V:
					DeleteSelected();
					Paste();
					break;

				case KeyCode.X:
					CopySelected();
					DeleteSelected();
					break;

				case KeyCode.Z:
					Undo();
					break;

				case KeyCode.Enter:
					if ( _multiline )
						InsertNewLine();

					if ( _sendNewLines )
						PostActionSignal( new Message( "TextNewLine" ) );

					break;

				case KeyCode.Home:
					GotoTextStart();
					break;

				case KeyCode.End:
					GotoTextEnd();
					break;

				case KeyCode.Left:
					GotoWordLeft();
					break;

				case KeyCode.Right:
					GotoWordRight();
					break;

				default:
					fallThrough = true;
					break;
			}
		}
		else if ( input.IsAltDown )
		{
			fallThrough = true;
		}
		else
		{
			switch ( code )
			{
				case KeyCode.Enter:
					if ( !_catchEnterKey )
					{
						fallThrough = true;
					}
					else if ( _multiline )
					{
						InsertNewLine();
					}
					else
					{
						_showKeyboardToggle = !_showKeyboardToggle;

						if ( _showKeyboardToggle )
							PostActionSignal( new Message( "Command" ) { Text = "ShowKeyboard" } );
					}

					if ( _sendNewLines )
						PostActionSignal( new Message( "TextNewLine" ) );

					break;

				case KeyCode.Backspace:
					if ( GetSelectedRange( out _, out _ ) )
						DeleteSelected();
					else
						Backspace();

					break;

				case KeyCode.Insert when shift:
					DeleteSelected();
					Paste();
					break;

				case KeyCode.Delete:
					if ( shift )
					{
						CopySelected();
						DeleteSelected();
					}
					else
					{
						DeleteChar();
					}

					break;

				case KeyCode.Home:
					if ( _multiline )
						GotoFirstOfLine();
					else
						GotoTextStart();

					break;

				case KeyCode.End:
					GotoEndOfLine();
					break;

				case KeyCode.PageUp:
					PageScroll( -1 );
					break;

				case KeyCode.PageDown:
					PageScroll( 1 );
					break;

				case KeyCode.Up when _multiline:
					GotoUp();
					break;

				case KeyCode.Down when _multiline:
					GotoDown();
					break;

				case KeyCode.Left:
					GotoLeft();
					break;

				case KeyCode.Right:
					GotoRight();
					break;

				case KeyCode.Insert:
				case KeyCode.Tab:
				case KeyCode.Escape:
				case KeyCode.LShift:
				case KeyCode.RShift:
				case KeyCode.Up:
				case KeyCode.Down:
				case >= KeyCode.F1 and <= KeyCode.F12:
					fallThrough = true;
					break;

				default:
					return;
			}
		}

		_selectEnd = selectEnd >= 0 ? selectEnd : _cursorPos;

		if ( _dataChanged )
			FireActionSignal();

		if ( !fallThrough )
			return;

		_putCursorAtEnd = _cursorIsAtEnd;

		base.OnKeyCodeTyped( code );
	}

	public override void OnKeyTyped( char character )
	{
		_cursorIsAtEnd = _putCursorAtEnd;
		_putCursorAtEnd = false;

		if ( !IsPrint( character ) || character == '\t' )
			return;

		if ( !IsEditable )
		{
			base.OnKeyTyped( character );
			return;
		}

		if ( character != '\0' )
		{
			DeleteSelected();
			SaveUndoState();
			InsertChar( character );
		}

		_selectEnd = _cursorPos;

		if ( _dataChanged )
			FireActionSignal();
	}

	public override void OnKeyFocusTicked()
	{
		var time = Vgui.TimeMillis;

		if ( _cursorNextBlinkTime >= time )
			return;

		_cursorBlink = !_cursorBlink;
		_cursorNextBlinkTime = time + CursorBlinkRate;
	}

	public override void OnMouseFocusTicked()
	{
		if ( _mouseDragSelection )
			OnCursorMoved( 0, 0 );
	}
}
