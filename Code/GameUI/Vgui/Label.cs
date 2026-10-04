using System;

namespace GoldSrc.GameUI;

public enum Alignment
{
	NorthWest,
	North,
	NorthEast,
	West,
	Center,
	East,
	SouthWest,
	South,
	SouthEast
}

public enum ColorState
{
	Normal,
	Dull,
	Bright,
	BrightBase
}

public class Label : Panel
{
	private const string DefaultFont = "Default";

	private static readonly string[] AlignmentNames = ["north-west", "north", "north-east", "west", "center", "east", "south-west", "south", "south-east"];

	private sealed class ImageInfo
	{
		public Image Image;
		public int Offset;
		public int X = -1;
		public int Width = -1;
	}

	private readonly List<ImageInfo> _images = [];
	private readonly TextImage _textImage;
	private int _textImageIndex;

	private Alignment _contentAlignment = Alignment.West;
	private ColorState _textColorState;
	private int _textInsetX;
	private int _textInsetY;
	private Color32 _disabledFgColor1;
	private Color32 _disabledFgColor2;
	private Color32 _associateColor;
	private Panel _associate;
	private string _associateName;
	private string _fontOverrideName;
	private char _hotkey;
	private bool _scaleImages = true;
	private bool _wrap;

	public Label( Vgui vgui, Panel parent, string name, string text ) : base( vgui, parent, name )
	{
		_textImage = new TextImage( vgui, text ) { Color = default };
		_hotkey = CalculateHotkey( _textImage.Text );
		_textImageIndex = AddImage( _textImage, 0 );

		InvalidateLayout();
	}

	protected TextImage TextImage => _textImage;

	public string Text => _textImage.Text;

	private static char CalculateHotkey( string text )
	{
		for ( var i = 0; i < text.Length; i++ )
		{
			if ( text[i] != '&' )
				continue;

			i++;

			if ( i >= text.Length )
				return '\0';

			if ( text[i] != '&' && char.IsLetterOrDigit( text[i] ) )
				return char.ToLowerInvariant( text[i] );
		}

		return '\0';
	}

	public virtual void SetText( string text )
	{
		_textImage.SetText( text );
		_hotkey = CalculateHotkey( _textImage.Text );

		InvalidateLayout();
	}

	public void SetScaleImages( bool state ) => _scaleImages = state;

	public void SetContentAlignment( Alignment alignment ) => _contentAlignment = alignment;

	public void SetTextInset( int x, int y )
	{
		_textInsetX = x;
		_textInsetY = y;

		_textImage.SetDrawWidthOnly( Wide - _textInsetX );
	}

	public void SetFont( SchemeFont font ) => _textImage.SetFont( font );

	public SchemeFont Font => _textImage.Font;

	public void SetTextColorState( ColorState state )
	{
		if ( _textColorState == state )
			return;

		_textColorState = state;

		InvalidateLayout();
	}

	public int AddImage( Image image, int offset )
	{
		_images.Add( new ImageInfo { Image = image, Offset = offset } );

		InvalidateLayout();

		return _images.Count - 1;
	}

	public int SetTextImageIndex( int newIndex )
	{
		var oldIndex = _textImageIndex;

		if ( newIndex == oldIndex )
			return oldIndex;

		while ( _images.Count <= newIndex )
			AddImage( null, 0 );

		if ( oldIndex >= 0 && oldIndex < _images.Count )
			_images[oldIndex].Image = null;

		if ( newIndex >= 0 )
			_images[newIndex].Image = _textImage;

		_textImageIndex = newIndex;

		return oldIndex;
	}

	public int ImageCount => _images.Count;

	public Image GetImageAtIndex( int index ) => index >= 0 && index < _images.Count ? _images[index].Image : null;

	public void ClearImages()
	{
		_images.Clear();
		_textImageIndex = -1;
	}

	public void SetImageBounds( int index, int x, int width )
	{
		_images[index].X = x;
		_images[index].Width = width;
	}

	public void SetImageAtIndex( int index, Image image, int offset )
	{
		while ( _images.Count <= index )
			AddImage( null, 0 );

		if ( _images[index].Image == image && _images[index].Offset == offset )
			return;

		_images[index].Image = image;
		_images[index].Offset = offset;

		InvalidateLayout();
	}

	public override void SetFgColor( Color32 color )
	{
		if ( GetFgColor().Equals( color ) )
			return;

		base.SetFgColor( color );

		_textImage.Color = color;
	}

	public override Panel HasHotkey( char key ) => _hotkey == key ? this : null;

	public (int Wide, int Tall) GetContentSize()
	{
		if ( Font is null )
			SetFont( GetFont( DefaultFont ) );

		var (x0, y0, x1, y1) = ComputeAlignment();
		var wide = (_textInsetX - x0) + x1;

		wide -= _textImage.GetSize().Wide;
		wide += _textImage.GetContentSize().Wide;

		foreach ( var image in _images )
			wide += image.Offset;

		return (wide, (_textInsetY - y0) + y1);
	}

	private (int Wide, int Tall) ImageSize( Image image )
	{
		var (wide, tall) = image.GetSize();

		return image == _textImage || !_scaleImages ? (wide, tall) : (Scaled( wide ), Scaled( tall ));
	}

	private (int X0, int Y0, int X1, int Y1) ComputeAlignment()
	{
		var (wide, tall) = PaintSize;
		var contentWide = 0;
		var contentTall = 0;
		var alignment = _contentAlignment;

		for ( var i = 0; i < _images.Count; i++ )
		{
			var info = _images[i];

			if ( info.Image is null )
				continue;

			var (imageWide, imageTall) = ImageSize( info.Image );

			if ( wide < imageWide )
				alignment = Alignment.West;

			contentTall = Math.Max( contentTall, imageTall );
			contentWide += info.Offset + imageWide;
		}

		var x0 = ((int)alignment % 3) switch
		{
			1 => (wide - contentWide) / 2,
			2 => wide - contentWide,
			_ => 0
		};

		var y0 = ((int)_contentAlignment / 3) switch
		{
			1 => (tall - contentTall) / 2,
			2 => tall - contentTall,
			_ => 0
		};

		return (x0, y0, x0 + contentWide, y0 + contentTall);
	}

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "Hotkey":
				OnHotkeyPressed();
				break;

			case "SetText":
				SetText( message.Text );
				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}

	protected virtual void OnHotkeyPressed() => _associate?.RequestFocus();

	public override void OnMousePressed( MouseCode code ) => _associate?.RequestFocus();

	public override void OnRequestFocus( Panel subFocus, Panel defaultPanel )
	{
		if ( _associate is not null && subFocus == this )
			_associate.RequestFocus();
		else
			base.OnRequestFocus( subFocus, defaultPanel );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		if ( _fontOverrideName is not null )
			SetFont( GetFont( _fontOverrideName ) );

		if ( Font is null )
			SetFont( GetFont( DefaultFont ) );

		if ( _wrap )
		{
			_textImage.SetSize( Wide - _textInsetX, Tall );
			_textImage.RecalculateNewLinePositions();
		}
		else
		{
			_textImage.ResizeImageToContent();
		}

		for ( var i = 0; i < _images.Count; i++ )
		{
			if ( i != _textImageIndex )
				_images[i].Image = null;
		}

		_disabledFgColor1 = GetSchemeColor( "DisabledFgColor1" );
		_disabledFgColor2 = GetSchemeColor( "DisabledFgColor2" );

		SetBgColor( GetSchemeColor( "LabelBgColor" ) );

		SetFgColor( GetSchemeColor( _textColorState switch
		{
			ColorState.Dull => "LabelDimText",
			ColorState.Bright => "BrightControlText",
			ColorState.BrightBase => "BrightBaseText",
			_ => "BaseText"
		} ) );

		_associateColor = GetSchemeColor( "BrightControlText" );
	}

	public override void ApplySettings( KeyValues settings )
	{
		base.ApplySettings( settings );

		if ( settings.FindKey( "labelText" )?.Value is { } labelText )
			SetText( labelText );

		var alignment = Array.FindIndex( AlignmentNames, x => x.Equals( settings.GetString( "textAlignment" ), StringComparison.OrdinalIgnoreCase ) );

		if ( alignment >= 0 )
			SetContentAlignment( (Alignment)alignment );

		if ( settings.GetString( "associate" ) is { Length: > 0 } associate )
			_associateName = associate;

		if ( settings.GetInt( "dulltext" ) == 1 )
			SetTextColorState( ColorState.Dull );
		else
			SetTextColorState( settings.GetInt( "brighttext" ) == 1 ? ColorState.Bright : ColorState.Normal );

		if ( settings.GetString( "font" ) is { Length: > 0 } font )
		{
			_fontOverrideName = font;

			SetFont( GetFont( font ) );
		}
		else if ( _fontOverrideName is not null )
		{
			_fontOverrideName = null;

			SetFont( GetFont( DefaultFont ) );
		}

		_wrap = settings.GetInt( "wrap" ) > 0;
		_textImage.SetWrap( _wrap );

		InvalidateLayout( true );
	}

	public override void PerformLayout() => LayoutLabel();

	protected void LayoutLabel()
	{
		var wide = Wide - _textInsetX;

		if ( _images.Count == 1 && _textImageIndex == 0 )
		{
			var (contentWide, contentTall) = _textImage.GetContentSize();

			_textImage.SetSize( _wrap ? wide : Math.Min( contentWide, wide ), contentTall );
			return;
		}

		var widthOfImages = 0;

		for ( var i = 0; i < _images.Count; i++ )
		{
			if ( _images[i].Image is { } image && i != _textImageIndex )
				widthOfImages += ImageSize( image ).Wide;
		}

		var spaceAvailable = wide - widthOfImages;

		if ( spaceAvailable >= 0 )
			_textImage.SetSize( spaceAvailable, _textImage.GetSize().Tall );
	}

	public override void Paint() => PaintLabel();

	protected void PaintLabel()
	{
		var surface = Vgui.Surface;
		var (x0, y0, _, y1) = ComputeAlignment();

		if ( _associateName is not null )
		{
			var associate = Parent?.FindChildByName( _associateName );

			_associate = associate == this ? null : associate;
			_associateName = null;
		}

		var x = x0;
		var y = _textInsetY + y0;

		for ( var i = 0; i < _images.Count; i++ )
		{
			var info = _images[i];

			if ( info.Image is not { } image )
				continue;

			var (originalWide, originalTall) = image.GetSize();
			var (imageWide, imageTall) = ImageSize( image );

			if ( image != _textImage )
				image.SetSize( imageWide, imageTall );

			x += info.Offset;

			if ( i == _textImageIndex )
				x += _textInsetX;

			var imageX = info.X >= 0 ? info.X : x;
			var imageY = y;

			image.SetPos( imageX, imageY );

			if ( _contentAlignment is Alignment.West or Alignment.Center or Alignment.East && imageTall < y1 - y0 )
			{
				var space = (y1 - imageTall) - y0;

				if ( space < 0 )
					space++;

				imageY = y + (space >> 1);

				image.SetPos( imageX, imageY );
			}

			if ( info.Width >= 0 && info.Width < imageWide )
				image.SetSize( info.Width, imageTall );

			if ( image != _textImage )
			{
				image.Paint( surface );
			}
			else if ( !Enabled )
			{
				var offset = Scaled( 1 );

				_textImage.SetPos( imageX + offset, imageY + offset );
				_textImage.Color = _disabledFgColor1;
				_textImage.Paint( surface );

				_textImage.SetPos( imageX, imageY );
				_textImage.Color = _disabledFgColor2;
				_textImage.Paint( surface );
			}
			else if ( _associate is not null && Vgui.Input.Focus is { } focus && focus.HasParent( _associate ) )
			{
				_textImage.Color = _associateColor;
				_textImage.Paint( surface );
			}
			else
			{
				_textImage.Color = GetFgColor();
				_textImage.Paint( surface );
			}

			x = imageX + imageWide;

			image.SetSize( originalWide, originalTall );
		}
	}
}
