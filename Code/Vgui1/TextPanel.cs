using System;

namespace GoldSrc.Vgui1;

public class TextPanel : Panel
{
	private readonly TextImage _textImage;

	public TextPanel( string text, int x, int y, int wide, int tall ) : base( x, y, wide, tall )
	{
		_textImage = new TextImage( text );
		_textImage.SetSize( wide, tall );
	}

	public virtual void SetText( string text ) => _textImage.SetText( text );

	public virtual void SetFont( SchemeFont schemeFont ) => _textImage.SetFont( schemeFont );

	public virtual void SetFont( Font font ) => _textImage.SetFont( font );

	public override void SetSize( int wide, int tall )
	{
		base.SetSize( wide, tall );

		GetPaintSize( out wide, out tall );

		_textImage.SetSize( wide, tall );
	}

	public override void SetFgColor( int r, int g, int b, int a )
	{
		base.SetFgColor( r, g, b, a );

		_textImage.SetColor( new Color( r, g, b, a ) );
	}

	public override void SetFgColor( SchemeColor sc )
	{
		base.SetFgColor( sc );

		_textImage.SetColor( new Color( sc ) );
	}

	public virtual TextImage GetTextImage() => _textImage;

	public override void Paint()
	{
		_textImage.DoPaint( this );
	}
}

public class TextGrid : Panel
{
	private const int CellSize = 7;
	private const int PrintBufferLength = 2048;
	private const char MeasureChar = 'W';

	protected int _gridX;
	protected int _gridY;
	protected byte[] _grid;
	protected int _gridWide;
	protected int _gridTall;

	public TextGrid( int gridWide, int gridTall, int x, int y, int wide, int tall ) : base( x, y, wide, tall )
	{
		_grid = null;
		_gridWide = 0;
		_gridTall = 0;
		_gridX = 0;
		_gridY = 0;

		base.SetBgColor( 255, 255, 255, 0 );
		base.SetFgColor( 0, 0, 0, 0 );

		if ( gridWide > 0 && gridTall > 0 )
		{
			_grid = new byte[gridWide * gridTall * CellSize];
			_gridTall = gridTall;
			_gridWide = gridWide;
		}
	}

	public virtual void SetGridSize( int wide, int tall )
	{
		if ( wide <= 0 || tall <= 0 )
			return;

		_grid = new byte[wide * tall * CellSize];
		_gridTall = tall;
		_gridWide = wide;
	}

	public virtual void NewLine()
	{
		if ( _gridY != _gridTall - 1 )
		{
			_gridX = 0;
			_gridY++;
			return;
		}

		if ( _gridTall > 1 )
		{
			Array.Clear( _grid, ((_gridWide * _gridY) + _gridX) * CellSize, _gridWide - _gridX );

			for ( var row = 1; row < _gridTall; row++ )
				Array.Copy( _grid, _gridWide * row * CellSize, _grid, (row - 1) * _gridWide * CellSize, _gridWide * CellSize );

			Array.Clear( _grid, _gridY * _gridWide * CellSize, _gridWide * CellSize );
		}

		_gridX = 0;
	}

	public virtual void SetXY( int x, int y )
	{
		_gridX = x;
		_gridY = y;
	}

	public virtual int Printf( string text )
	{
		for ( var i = 0; i < PrintBufferLength && i < text.Length; i++ )
		{
			var ch = text[i];

			if ( ch == '\0' )
				break;

			if ( ch == '\n' )
			{
				NewLine();
				continue;
			}

			if ( _gridX < 0 || _gridX >= _gridWide || _gridY < 0 || _gridY >= _gridTall )
				continue;

			_grid[((_gridY * _gridWide) + _gridX) * CellSize] = (byte)ch;
			_gridX++;
		}

		Repaint();

		return text.Length;
	}

	public override void PaintBackground()
	{
		base.PaintBackground();

		var font = GetApp().GetScheme().GetFont( SchemeFont.Primary2 );

		font.GetCharAbcWide( MeasureChar, out var a, out var b, out var c );

		var cellWide = a + b + c;
		var cellTall = font.Tall;

		DrawSetTextFont( SchemeFont.Primary2 );

		var y = 0;

		for ( var row = 0; row < _gridTall; row++ )
		{
			var x = 0;

			for ( var column = 0; column < _gridWide; column++ )
			{
				var ch = _grid[((_gridWide * row) + column) * CellSize];

				if ( ch != 0 )
				{
					GetFgColor( out var r, out var g, out var blue, out var alpha );

					DrawSetTextColor( r, g, blue, alpha );
					DrawPrintChar( x, y, (char)ch );
				}

				x += cellWide;
			}

			y += cellTall;
		}
	}
}

public class HeaderPanel : Panel
{
	private sealed class SliderHandler( HeaderPanel headerPanel ) : InputSignal
	{
		public void CursorMoved( int x, int y, Panel panel ) => headerPanel.PrivateCursorMoved( x, y, panel );

		public void CursorEntered( Panel panel )
		{
		}

		public void CursorExited( Panel panel )
		{
		}

		public void MousePressed( MouseCode code, Panel panel ) => headerPanel.PrivateMousePressed( code, panel );

		public void MouseDoublePressed( MouseCode code, Panel panel )
		{
		}

		public void MouseReleased( MouseCode code, Panel panel ) => headerPanel.PrivateMouseReleased( code, panel );

		public void MouseWheeled( int delta, Panel panel )
		{
		}

		public void KeyPressed( KeyCode code, Panel panel )
		{
		}

		public void KeyTyped( KeyCode code, Panel panel )
		{
		}

		public void KeyReleased( KeyCode code, Panel panel )
		{
		}

		public void KeyFocusTicked( Panel panel )
		{
		}
	}

	private const int DefaultSliderWide = 11;

	private readonly List<Panel> _sliderPanelDar = [];
	private readonly List<Panel> _sectionPanelDar = [];
	private readonly List<ChangeSignal> _changeSignalDar = [];
	private readonly Panel _sectionLayer;
	private readonly int _sliderWide;
	private bool _dragging;
	private int _dragSliderIndex;
	private int _dragSliderStartPos;
	private int _dragSliderStartX;

	public HeaderPanel( int x, int y, int wide, int tall ) : base( x, y, wide, tall )
	{
		_sliderWide = DefaultSliderWide;
		_dragging = false;

		_sectionLayer = new Panel( 0, 0, wide, tall );
		_sectionLayer.SetPaintBorderEnabled( false );
		_sectionLayer.SetPaintBackgroundEnabled( false );
		_sectionLayer.SetPaintEnabled( false );
		_sectionLayer.SetParent( this );
	}

	public override void PerformLayout()
	{
		GetPaintSize( out var wide, out var tall );

		_sectionLayer.SetBounds( 0, 0, wide, tall );

		var x0 = 0;

		for ( var i = 0; i < _sectionPanelDar.Count; i++ )
		{
			_sliderPanelDar[i].GetPos( out var x, out _ );

			var x1 = x + (_sliderWide / 2);

			_sectionPanelDar[i].SetBounds( x0, 0, x1 - x0, tall );

			x0 = x1;
		}
	}

	public virtual void AddSectionPanel( Panel panel )
	{
		InvalidateLayout( true );

		var x = 0;
		var y = 0;
		var wide = 0;
		var tall = 0;

		for ( var i = 0; i < _sectionPanelDar.Count; i++ )
		{
			_sectionPanelDar[i].GetBounds( out x, out y, out wide, out tall );

			x += _sliderWide + wide;
		}

		_sectionPanelDar.Add( panel );

		panel.SetPos( x, 0 );
		panel.SetParent( _sectionLayer );
		panel.SetBounds( x, y, wide, tall );

		GetPaintSize( out wide, out tall );

		var slider = new Panel( 0, 0, _sliderWide, tall );

		slider.SetPaintBorderEnabled( false );
		slider.SetPaintBackgroundEnabled( false );
		slider.SetPaintEnabled( false );
		slider.SetPos( x + wide, 0 );
		slider.AddInputSignal( new SliderHandler( this ) );
		slider.SetCursor( GetApp().GetScheme().GetCursor( SchemeCursor.SizeWe ) );
		slider.SetParent( this );

		_sliderPanelDar.Add( slider );

		InvalidateLayout( false );
		FireChangeSignal();
		Repaint();
	}

	public virtual void SetSliderPos( int sliderIndex, int pos )
	{
		_sliderPanelDar[sliderIndex].SetPos( pos - (_sliderWide / 2), 0 );

		InvalidateLayout( false );
		FireChangeSignal();
		Repaint();
	}

	public virtual int GetSectionCount() => _sectionPanelDar.Count;

	public virtual void GetSectionExtents( int sectionIndex, out int x0, out int x1 )
	{
		_sectionPanelDar[sectionIndex].GetBounds( out var x, out _, out var wide, out _ );

		x0 = x;
		x1 = x + wide;
	}

	public virtual void AddChangeSignal( ChangeSignal s )
	{
		if ( !_changeSignalDar.Contains( s ) )
			_changeSignalDar.Add( s );
	}

	public virtual void FireChangeSignal()
	{
		InvalidateLayout( true );

		for ( var i = 0; i < _changeSignalDar.Count; i++ )
			_changeSignalDar[i].ValueChanged( this );
	}

	public virtual void PrivateCursorMoved( int x, int y, Panel panel )
	{
		if ( !_dragging )
			return;

		GetApp().GetCursorPos( out x, out y );
		ScreenToLocal( ref x, ref y );

		SetSliderPos( _dragSliderIndex, _dragSliderStartPos - _dragSliderStartX + x );
		InvalidateLayout( false );
		Repaint();
	}

	public virtual void PrivateMousePressed( MouseCode code, Panel panel )
	{
		GetApp().GetCursorPos( out var mouseX, out var mouseY );
		ScreenToLocal( ref mouseX, ref mouseY );

		for ( var i = 0; i < _sliderPanelDar.Count; i++ )
		{
			var slider = _sliderPanelDar[i];

			if ( slider != panel )
				continue;

			slider.GetPos( out var x, out _ );

			_dragging = true;
			_dragSliderIndex = i;
			_dragSliderStartPos = (_sliderWide / 2) + x;
			_dragSliderStartX = mouseX;

			slider.SetAsMouseCapture( true );

			return;
		}
	}

	public virtual void PrivateMouseReleased( MouseCode code, Panel panel )
	{
		_dragging = false;

		panel.SetAsMouseCapture( false );
	}
}
