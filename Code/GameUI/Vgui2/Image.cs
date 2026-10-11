namespace GoldSrc.GameUI;

public abstract class Image
{
	public int X { get; private set; }

	public int Y { get; private set; }

	public int Wide { get; protected set; }

	public int Tall { get; protected set; }

	public Color32 Color { get; set; } = new( 255, 255, 255, 255 );

	public void SetPos( int x, int y )
	{
		X = x;
		Y = y;
	}

	public virtual void SetSize( int wide, int tall )
	{
		Wide = wide;
		Tall = tall;
	}

	public virtual (int Wide, int Tall) GetSize() => (Wide, Tall);

	public virtual (int Wide, int Tall) GetContentSize() => (0, 0);

	public abstract void Paint( Surface surface );
}

public sealed class BitmapImage( Texture texture ) : Image
{
	public override (int Wide, int Tall) GetSize() => texture is null ? (0, 0) : (texture.Width, texture.Height);

	public override (int Wide, int Tall) GetContentSize() => GetSize();

	public override void Paint( Surface surface )
	{
		if ( texture is null )
			return;

		if ( Wide == 0 )
			(Wide, Tall) = GetSize();

		surface.DrawSetColor( Color );
		surface.DrawTexturedRect( texture, X, Y, X + Wide, Y + Tall );
	}
}

public sealed class TextImage : Image
{
	private const char HotkeyMarker = '&';
	private const int EllipsisDots = 3;

	private readonly Vgui _vgui;
	private readonly List<int> _lineBreaks = [];

	private string _text = "";
	private SchemeFont _font;
	private int _drawWidth;
	private int _ellipsisPosition = -1;
	private bool _recalculateTruncation;
	private bool _wrap;

	public TextImage( Vgui vgui, string text )
	{
		_vgui = vgui;

		SetText( text );
	}

	public string Text => _text;

	public SchemeFont Font => _font;

	public void SetText( string text )
	{
		_text = _vgui.Localize( text );
		_recalculateTruncation = true;
	}

	public void SetFont( SchemeFont font )
	{
		_font = font;
		_recalculateTruncation = true;
	}

	public void SetWrap( bool state ) => _wrap = state;

	public void RecalculateNewLinePositions()
	{
		var x = 0;
		var wordStart = 0;
		var hasWord = false;
		var justStartedNewLine = true;
		var wordStartedOnNewLine = true;

		_lineBreaks.Clear();

		for ( var i = _text.Length > 0 && _text[0] is '\r' or '\n' ? 1 : 0; i < _text.Length; i++ )
		{
			var character = _text[i];

			if ( char.IsWhiteSpace( character ) )
			{
				hasWord = false;
			}
			else if ( !hasWord )
			{
				hasWord = true;
				wordStartedOnNewLine = justStartedNewLine;
				wordStart = i;
			}

			x += Surface.GetCharacterWidth( _font, character );

			if ( !char.IsControl( character ) )
				justStartedNewLine = false;

			if ( x < _drawWidth && character is not ('\r' or '\n') )
				continue;

			justStartedNewLine = true;
			hasWord = false;

			if ( character is not ('\r' or '\n') )
			{
				if ( !wordStartedOnNewLine )
					i = wordStart;

				_lineBreaks.Add( i );
			}

			x = 0;
		}
	}

	public void SetDrawWidthOnly( int width ) => _drawWidth = width;

	public void SetDrawWidth( int width )
	{
		_drawWidth = width;

		if ( _recalculateTruncation && _font is not null )
			RecalculateEllipsesPosition();

		ResizeImageToContent();
	}

	public override void SetSize( int wide, int tall )
	{
		base.SetSize( wide, tall );

		_drawWidth = wide;
		_recalculateTruncation = true;
	}

	public void ResizeImageToContent()
	{
		var (wide, tall) = GetContentSize();

		SetSize( wide, tall );
	}

	public override (int Wide, int Tall) GetContentSize()
	{
		if ( _font is null )
			return (0, 0);

		var fontTall = Surface.GetFontTall( _font );
		var wide = 0;
		var maxWide = 0;
		var tall = fontTall;

		if ( _wrap )
			RecalculateNewLinePositions();

		for ( var i = 0; i < _text.Length; i++ )
		{
			if ( _text[i] == HotkeyMarker && i + 1 < _text.Length )
				i++;

			wide += Surface.GetCharacterWidth( _font, _text[i] );

			if ( _text[i] == '\n' )
			{
				tall += fontTall;
				maxWide = System.Math.Max( maxWide, wide );
				wide = 0;
			}

			if ( !_wrap || !_lineBreaks.Contains( i ) )
				continue;

			tall += fontTall;
			maxWide = System.Math.Max( maxWide, wide );
			wide = 0;
		}

		return (System.Math.Max( wide, maxWide ), tall);
	}

	private void RecalculateEllipsesPosition()
	{
		_recalculateTruncation = false;
		_ellipsisPosition = -1;

		if ( _wrap || _text.Contains( '\n' ) )
			return;

		var dotWidth = Surface.GetCharacterWidth( _font, '.' );
		var x = 0;

		for ( var i = 0; i < _text.Length; i++ )
		{
			var character = _text[i];

			if ( character == '\r' )
				continue;

			if ( character == HotkeyMarker )
			{
				if ( i + 1 >= _text.Length || _text[i + 1] != HotkeyMarker )
					continue;

				i++;
			}

			if ( i == 0 )
				continue;

			var width = Surface.GetCharacterWidth( _font, character );
			var start = x;

			x += width;

			if ( _drawWidth >= (dotWidth * EllipsisDots) + x )
				continue;

			var remaining = width;

			for ( var j = i + 1; j < _text.Length; j++ )
			{
				if ( _text[j] == '\r' )
					continue;

				if ( _text[j] == HotkeyMarker )
				{
					if ( j + 1 >= _text.Length || _text[j + 1] != HotkeyMarker )
						continue;

					j++;
				}

				remaining += Surface.GetCharacterWidth( _font, _text[j] );
			}

			if ( _drawWidth >= start + remaining )
				continue;

			_ellipsisPosition = i;
			return;
		}
	}

	public override void Paint( Surface surface )
	{
		if ( _text.Length == 0 || _font is null )
			return;

		if ( _recalculateTruncation )
			RecalculateEllipsesPosition();

		surface.DrawSetTextColor( Color );
		surface.DrawSetTextFont( _font );

		var fontTall = Surface.GetFontTall( _font );
		var x = 0;
		var y = 0;
		var lineBreak = 0;

		for ( var i = 0; i < _text.Length; i++ )
		{
			var character = _text[i];

			if ( character == '\r' )
				continue;

			if ( character == '\n' )
			{
				y += fontTall;
				x = 0;
				continue;
			}

			if ( character == HotkeyMarker )
			{
				if ( i + 1 >= _text.Length || _text[i + 1] != HotkeyMarker )
					continue;

				i++;
			}

			if ( i == _ellipsisPosition )
			{
				for ( var dot = 0; dot < EllipsisDots; dot++ )
				{
					surface.DrawSetTextPos( X + x, Y + y );
					surface.DrawUnicodeChar( '.' );

					x += Surface.GetCharacterWidth( _font, '.' );
				}

				return;
			}

			if ( lineBreak != _lineBreaks.Count && i == _lineBreaks[lineBreak] )
			{
				y += fontTall;
				x = 0;
				lineBreak++;
			}

			surface.DrawSetTextPos( X + x, Y + y );
			surface.DrawUnicodeChar( character );

			x += Surface.GetCharacterWidth( _font, character );
		}
	}
}
