using System;

namespace GoldSrc.Vgui1;

public class TextImage : Image
{
	private string _text;
	private int _textBufferLen;
	private SchemeFont _schemeFont;
	private Font _font;

	public TextImage( int textBufferLen, string text )
	{
		Init( text );
	}

	public TextImage( string text )
	{
		Init( text );
	}

	private void Init( string text )
	{
		_text = null;
		_font = null;
		_textBufferLen = 0;
		_schemeFont = SchemeFont.Primary1;

		TextImageSetText( text.Length + 1, text );
		TextImageGetTextSize( out var wide, out var tall );

		_wide = wide;
		_tall = tall;
	}

	public virtual void GetTextSize( out int wide, out int tall ) => TextImageGetTextSize( out wide, out tall );

	private void TextImageGetTextSize( out int wide, out int tall )
	{
		wide = 0;
		tall = 0;

		if ( _text is null )
			return;

		TextImageGetFont()?.GetTextSize( _text, out wide, out tall );
	}

	public virtual void GetTextSizeWrapped( out int wide, out int tall )
	{
		wide = 0;
		tall = 0;

		if ( _text is null )
			return;

		GetSize( out var sizeWide, out _ );

		var font = GetFont();
		var yAdd = font.Tall;
		var x = 0;
		var y = yAdd;

		for ( var i = 0; ; i++ )
		{
			var ch = CharAt( i );

			font.GetCharAbcWide( (sbyte)ch, out var a, out var b, out var c );

			var len = a + b + c;

			if ( ch == '\0' )
				break;

			if ( ch == '\r' )
				continue;

			if ( ch == '\n' )
			{
				x = 0;
				y += yAdd;
				continue;
			}

			if ( ch == ' ' )
			{
				var next = CharAt( i + 1 );

				font.GetCharAbcWide( ' ', out a, out b, out c );

				if ( next != '\0' && next != '\n' && next != '\r' )
				{
					x += a + b + c;

					if ( x > sizeWide )
					{
						x = 0;
						y += yAdd;
					}
				}

				continue;
			}

			var count = 1;

			for ( ; ; count++ )
			{
				ch = CharAt( i + count );

				if ( ch == '\0' || ch == '\n' || ch == '\r' || ch == ' ' )
					break;

				font.GetCharAbcWide( (sbyte)ch, out a, out b, out c );

				len += a + b + c;
			}

			if ( x + len > sizeWide )
			{
				x = 0;
				y += yAdd;
			}

			for ( var j = 0; j < count; j++ )
			{
				font.GetCharAbcWide( (sbyte)CharAt( i + j ), out a, out b, out c );

				x += a + b + c;

				if ( x > wide )
					wide = x;

				if ( y > tall )
					tall = y;
			}

			i += count - 1;
		}
	}

	public virtual Font GetFont() => TextImageGetFont();

	private Font TextImageGetFont() => _font ?? App.GetInstance().GetScheme().GetFont( _schemeFont );

	public virtual void SetText( int textBufferLen, string text ) => TextImageSetText( textBufferLen, text );

	private void TextImageSetText( int textBufferLen, string text )
	{
		if ( _textBufferLen < textBufferLen )
			_textBufferLen = textBufferLen;

		_text = text.Length > _textBufferLen - 1 ? text[..Math.Max( _textBufferLen - 1, 0 )] : text;

		GetTextSize( out var wide, out var tall );

		_wide = wide;
		_tall = tall;
	}

	public virtual void SetText( string text ) => SetText( text.Length + 1, text );

	public virtual void SetFont( SchemeFont schemeFont )
	{
		_schemeFont = schemeFont;
	}

	public virtual void SetFont( Font font )
	{
		_font = font;
	}

	public override void SetSize( int wide, int tall )
	{
		_wide = wide;
		_tall = tall;
	}

	private char CharAt( int index ) => index < _text.Length ? _text[index] : '\0';

	protected override void Paint( Panel panel )
	{
		GetSize( out var wide, out _ );

		if ( _text is null )
			return;

		GetColor( out var color );
		color.GetColor( out var r, out var g, out var blue, out var alpha );

		DrawSetTextColor( r, g, blue, alpha );

		var font = GetFont();

		DrawSetTextFont( font );

		var yAdd = font.Tall;
		var x = 0;
		var y = 0;

		for ( var i = 0; ; i++ )
		{
			var ch = CharAt( i );

			font.GetCharAbcWide( (sbyte)ch, out var a, out var b, out var c );

			var len = a + b + c;

			if ( ch == '\0' )
				break;

			if ( ch == '\r' )
				continue;

			if ( ch == '\n' )
			{
				x = 0;
				y += yAdd;
				continue;
			}

			if ( ch == ' ' )
			{
				var next = CharAt( i + 1 );

				font.GetCharAbcWide( ' ', out a, out b, out c );

				if ( next != '\0' && next != '\n' && next != '\r' )
				{
					x += a + b + c;

					if ( x > wide )
					{
						x = 0;
						y += yAdd;
					}
				}

				continue;
			}

			var count = 1;

			for ( ; ; count++ )
			{
				ch = CharAt( i + count );

				if ( ch == '\0' || ch == '\n' || ch == '\r' || ch == ' ' )
					break;

				font.GetCharAbcWide( (sbyte)ch, out a, out b, out c );

				len += a + b + c;
			}

			if ( x + len > wide )
			{
				x = 0;
				y += yAdd;
			}

			for ( var j = 0; j < count; j++ )
			{
				ch = CharAt( i + j );

				font.GetCharAbcWide( (sbyte)ch, out a, out b, out c );

				DrawPrintChar( x, y, ch );

				x += a + b + c;
			}

			i += count - 1;
		}
	}
}

public class LineBorder : Border
{
	private Color _color;

	public LineBorder()
	{
		Init( 1, new Color( 0, 0, 0, 0 ) );
	}

	public LineBorder( int thickness )
	{
		Init( thickness, new Color( 0, 0, 0, 0 ) );
	}

	public LineBorder( Color color )
	{
		Init( 1, color );
	}

	public LineBorder( int thickness, Color color )
	{
		Init( thickness, color );
	}

	public void SetLineColor( int r, int g, int b, int a )
	{
		_color = new Color( r, g, b, a );
	}

	private void Init( int thickness, Color color )
	{
		_left = thickness;
		_top = thickness;
		_right = thickness;
		_bottom = thickness;
		_color = color;
	}

	protected override void Paint( Panel panel )
	{
		panel.GetSize( out var wide, out var tall );

		_color.GetColor( out var r, out var g, out var b, out var a );

		DrawSetColor( r, g, b, a );
		DrawFilledRect( 0, 0, wide, _top );
		DrawFilledRect( 0, tall - _bottom, wide, tall );
		DrawFilledRect( 0, _top, _left, tall - _bottom );
		DrawFilledRect( wide - _right, _top, wide, tall - _bottom );
	}
}

public class Label : Panel
{
	public enum Alignment
	{
		Northwest,
		North,
		Northeast,
		West,
		Center,
		East,
		Southwest,
		South,
		Southeast
	}

	private const int DefaultSize = 10;
	private const int ContentMargin = 4;

	protected bool _contentFitted;
	protected Alignment _textAlignment;
	protected Alignment _contentAlignment;
	protected TextImage _textImage;
	protected Image _image;

	public Label( int textBufferLen, string text, int x, int y, int wide, int tall ) : base( x, y, wide, tall )
	{
		Init( textBufferLen, text, false );
	}

	public Label( string text, int x, int y, int wide, int tall ) : base( x, y, wide, tall )
	{
		Init( text.Length + 1, text, false );
	}

	public Label( string text, int x, int y ) : base( x, y, DefaultSize, DefaultSize )
	{
		Init( text.Length + 1, text, true );
	}

	public Label( string text ) : base( 0, 0, DefaultSize, DefaultSize )
	{
		Init( text.Length + 1, text, true );
	}

	public Label() : base( 0, 0, DefaultSize, DefaultSize )
	{
		Init( 1, "", true );
	}

	private void Init( int textBufferLen, string text, bool textFitted )
	{
		_contentFitted = textFitted;
		_textAlignment = Alignment.Center;
		_contentAlignment = Alignment.Center;
		_textImage = new TextImage( textBufferLen, text );
		_textImage.SetColor( new Color( SchemeColor.Black ) );
		_image = null;

		_textImage.SetText( textBufferLen, text );

		LabelComputeAlignment( true, out _, out _, out _, out _, out _, out _, out _, out _, out var minX, out var minY, out var maxX, out var maxY );

		var wide = maxX - minX;
		var tall = maxY - minY;

		PanelSetPreferredSize( wide, tall );

		if ( _contentFitted )
			PanelSetSize( wide, tall );

		PanelRepaint();
	}

	public virtual void SetImage( Image image )
	{
		_image = image;

		RecomputeMinimumSize();

		if ( image is not null )
			Repaint();
	}

	public virtual void SetText( int textBufferLen, string text )
	{
		_textImage.SetText( textBufferLen, text );

		RecomputeMinimumSize();
		Repaint();
	}

	public virtual void SetText( string text ) => SetText( text.Length + 1, text );

	public virtual void SetFont( SchemeFont schemeFont )
	{
		_textImage.SetFont( schemeFont );

		RecomputeMinimumSize();
		Repaint();
	}

	public virtual void SetFont( Font font )
	{
		_textImage.SetFont( font );

		RecomputeMinimumSize();
		Repaint();
	}

	public virtual void GetTextSize( out int wide, out int tall ) => _textImage.GetTextSize( out wide, out tall );

	public virtual void GetContentSize( out int wide, out int tall )
	{
		ComputeAlignment( out _, out _, out _, out _, out _, out _, out _, out _, out var minX, out var minY, out var maxX, out var maxY );

		wide = maxX - minX;
		tall = maxY - minY;
	}

	public virtual void SetTextAlignment( Alignment alignment )
	{
		_textAlignment = alignment;

		RecomputeMinimumSize();
		Repaint();
	}

	public virtual void SetContentAlignment( Alignment alignment )
	{
		_contentAlignment = alignment;

		RecomputeMinimumSize();
		Repaint();
	}

	private sealed class TextProperty : Panel, ActionSignal
	{
		private const int BufferLength = 256;

		private readonly Label _label;
		private readonly TextEntry _textEntry;

		public TextProperty( Label label ) : base( 0, 0, 200, 20 )
		{
			_label = label;

			SetLayout( new FlowLayout( 2 ) );

			new Label( "setText" ).SetParent( this );

			_textEntry = new TextEntry( "", 0, 0, 80, 20 );
			_textEntry.SetParent( this );
			_textEntry.AddActionSignal( this );
		}

		public void ActionPerformed( Panel panel )
		{
			_label.SetText( _textEntry.GetText( 0, BufferLength ) );
			_label.Repaint();
		}
	}

	public override Panel CreatePropertyPanel()
	{
		var parentPanel = base.CreatePropertyPanel();
		var folder = new TreeFolder( "Label" );

		parentPanel.AddChild( folder );

		folder.AddChild( new TextProperty( this ) );
		folder.AddChild( new Label( "setContentAlignment" ) );

		return parentPanel;
	}

	public override void SetFgColor( int r, int g, int b, int a )
	{
		base.SetFgColor( r, g, b, a );

		_textImage.SetColor( new Color( r, g, b, a ) );

		Repaint();
	}

	public override void SetFgColor( SchemeColor sc )
	{
		base.SetFgColor( sc );

		_textImage.SetColor( new Color( sc ) );

		Repaint();
	}

	public virtual void SetContentFitted( bool state )
	{
		_contentFitted = state;

		RecomputeMinimumSize();
		Repaint();
	}

	protected virtual void ComputeAlignment( out int tx0, out int ty0, out int tx1, out int ty1, out int ix0, out int iy0, out int ix1, out int iy1, out int minX, out int minY, out int maxX, out int maxY )
	{
		LabelComputeAlignment( false, out tx0, out ty0, out tx1, out ty1, out ix0, out iy0, out ix1, out iy1, out minX, out minY, out maxX, out maxY );
	}

	private void LabelComputeAlignment( bool constructing, out int tx0, out int ty0, out int tx1, out int ty1, out int ix0, out int iy0, out int ix1, out int iy1, out int minX, out int minY, out int maxX, out int maxY )
	{
		int wide;
		int tall;
		int textWide;
		int textTall;

		if ( constructing )
		{
			PanelGetPaintSize( out wide, out tall );
			_textImage.GetTextSize( out textWide, out textTall );
		}
		else
		{
			GetPaintSize( out wide, out tall );
			GetTextSize( out textWide, out textTall );
		}

		var imageWide = 0;
		var imageTall = 0;

		_image?.GetSize( out imageWide, out imageTall );

		ix0 = 0;
		iy0 = 0;
		ix1 = imageWide;
		iy1 = imageTall;

		tx0 = 0;
		ty0 = 0;
		tx1 = textWide;
		ty1 = textTall;

		switch ( _textAlignment )
		{
			case Alignment.Northwest:
			case Alignment.West:
			case Alignment.Southwest:
				tx0 = ix0 - textWide;
				break;
			case Alignment.North:
			case Alignment.Center:
			case Alignment.South:
				tx0 = ((ix1 - ix0) / 2) - ((tx1 - tx0) / 2);
				break;
			case Alignment.Northeast:
			case Alignment.East:
			case Alignment.Southeast:
				tx0 = ix1;
				break;
		}

		switch ( _textAlignment )
		{
			case Alignment.Northwest:
			case Alignment.North:
			case Alignment.Northeast:
				ty0 = iy0 - textTall;
				break;
			case Alignment.West:
			case Alignment.Center:
			case Alignment.East:
				ty0 = ((iy1 - iy0) / 2) - ((ty1 - ty0) / 2);
				break;
			case Alignment.Southwest:
			case Alignment.South:
			case Alignment.Southeast:
				ty0 = iy1;
				break;
		}

		tx1 = tx0 + textWide;
		ty1 = ty0 + textTall;

		minX = tx0;
		minY = ty0;

		if ( ix0 < minX )
			minX = ix0;

		if ( iy0 < minY )
			minY = iy0;

		maxX = tx1;
		maxY = ty1;

		if ( maxX < ix1 )
			maxX = ix1;

		if ( maxY < iy1 )
			maxY = iy1;

		tx0 -= minX;
		ty0 -= minY;
		tx1 -= minX;
		ty1 -= minY;
		ix0 -= minX;
		iy0 -= minY;
		ix1 -= minX;
		iy1 -= minY;
		maxX -= minX;
		maxY -= minY;
		minX = 0;
		minY = 0;

		var offsetX = 0;
		var offsetY = 0;

		switch ( _contentAlignment )
		{
			case Alignment.Northwest:
			case Alignment.West:
			case Alignment.Southwest:
				offsetX = 0;
				break;
			case Alignment.North:
			case Alignment.Center:
			case Alignment.South:
				offsetX = (wide / 2) - ((maxX - minX) / 2);
				break;
			case Alignment.Northeast:
			case Alignment.East:
			case Alignment.Southeast:
				offsetX = minX - maxX + wide;
				break;
		}

		switch ( _contentAlignment )
		{
			case Alignment.Northwest:
			case Alignment.North:
			case Alignment.Northeast:
				offsetY = 0;
				break;
			case Alignment.West:
			case Alignment.Center:
			case Alignment.East:
				offsetY = (tall / 2) - (maxY / 2);
				break;
			case Alignment.Southwest:
			case Alignment.South:
			case Alignment.Southeast:
				offsetY = tall - maxY;
				break;
		}

		tx0 += offsetX;
		ty0 += offsetY;
		tx1 += offsetX;
		ty1 += offsetY;
		ix0 += offsetX;
		iy0 += offsetY;
		ix1 += offsetX;
		iy1 += offsetY;
		minX += offsetX;
		minY += offsetY;
		maxX += offsetX;
		maxY += offsetY;

		minX -= ContentMargin;
		minY -= ContentMargin;
		maxX += ContentMargin;
		maxY += ContentMargin;
	}

	public override void Paint()
	{
		ComputeAlignment( out var tx0, out var ty0, out var tx1, out var ty1, out var ix0, out var iy0, out _, out _, out _, out _, out _, out _ );

		if ( _image is not null )
		{
			_image.SetPos( ix0, iy0 );
			_image.DoPaint( this );
		}

		if ( _textImage is not null )
		{
			_textImage.SetPos( tx0, ty0 );
			_textImage.DoPaint( this );
		}

		if ( HasFocus() )
		{
			DrawSetColor( SchemeColor.Primary2 );
			DrawOutlinedRect( tx0, ty0, tx1, ty1 );
		}
	}

	protected virtual void RecomputeMinimumSize()
	{
		GetContentSize( out var wide, out var tall );

		SetPreferredSize( wide, tall );

		if ( _contentFitted )
			SetSize( wide, tall );
	}
}
