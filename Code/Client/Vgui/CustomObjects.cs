using System;
using System.Text;
using GoldSrc;
using GoldSrc.Vgui1;

namespace HalfLife;

public static class VguiUtil
{
	public const int ArrowUp = 0;
	public const int ArrowDown = 1;
	public const int ArrowLeft = 2;
	public const int ArrowRight = 3;

	[SkipHotload]
	public static readonly string[] ArrowFilenames =
	[
		"arrowup",
		"arrowdn",
		"arrowlt",
		"arrowrt"
	];

	public const int NumberOfTeamColors = 5;

	[SkipHotload]
	public static readonly int[,] TeamColors =
	{
		{ 255, 170, 0 },
		{ 125, 165, 210 },
		{ 200, 90, 70 },
		{ 225, 205, 45 },
		{ 145, 215, 140 }
	};

	public static HalfLifeClientDll Dll => (HalfLifeClientDll)ClientDll.Loaded;

	public static Hud Hud => Dll.Hud;

	public static TeamFortressViewport ViewPort => Hud.ViewPort;

	public static int ScreenWidth => Hud.ScreenWidth;

	public static int ScreenHeight => Hud.ScreenHeight;

	public static float XRes( float x ) => x * ((float)ScreenWidth / 640);

	public static float YRes( float y ) => y * ((float)ScreenHeight / 480);

	public static float XResHd( float x ) => x * Math.Max( 1f, ScreenWidth / 1280f );

	public static float YResHd( float y ) => y * Math.Max( 1f, ScreenHeight / 720f );

	public static float ButtonSizeY => YRes( 30 );

	public static float CMenuSizeX => XRes( 160 );

	public static float CMenuTop => ButtonSizeY * 4;

	public static string Truncate( string text, int size ) => text.Length >= size ? text[..(size - 1)] : text;

	public static string Format( string format, params object[] arguments )
	{
		var builder = new StringBuilder();
		var argument = 0;

		for ( var i = 0; i < format.Length; i++ )
		{
			if ( format[i] != '%' || i + 1 >= format.Length )
			{
				builder.Append( format[i] );
				continue;
			}

			i++;

			if ( format[i] == '%' )
				builder.Append( '%' );
			else if ( format[i] is 's' or 'd' or 'i' && argument < arguments.Length )
				builder.Append( arguments[argument++] );
		}

		return builder.ToString();
	}

	public static string GetTgaNameForRes( string name )
	{
		var i = ScreenWidth < 640 ? 320 : 640;

		return $"gfx/vgui/{name.Replace( "%d", i.ToString() )}.tga";
	}

	public static BitmapTGA LoadTgaForRes( string imageName )
	{
		return VguiLoadTga.LoadTga( GetTgaNameForRes( $"%d_{imageName}" ) );
	}
}

public class ImageLabel : Label
{
	public BitmapTGA Tga;

	public ImageLabel( string imageName, int x, int y ) : base( "", x, y )
	{
		SetContentFitted( true );
		Tga = VguiUtil.LoadTgaForRes( imageName );
		SetImage( Tga );
	}

	public ImageLabel( string imageName, int x, int y, int wide, int tall ) : base( "", x, y, wide, tall )
	{
		SetContentFitted( true );
		Tga = VguiUtil.LoadTgaForRes( imageName );
		SetImage( Tga );
	}

	public virtual int GetImageWide()
	{
		if ( Tga is not null )
		{
			Tga.GetSize( out var xSize, out _ );

			return xSize;
		}

		return 1;
	}

	public virtual int GetImageTall()
	{
		if ( Tga is not null )
		{
			Tga.GetSize( out _, out var ySize );

			return ySize;
		}

		return 1;
	}

	public void LoadImage( string imageName )
	{
		Tga = VguiUtil.LoadTgaForRes( imageName );

		if ( Tga is null )
		{
			var stream = new FileInputStream( $"{Server.GameDirectory}/{imageName}", false );

			Tga = new BitmapTGA( stream, true );
			stream.Close();
		}

		Tga.GetSize( out var w, out var t );

		SetSize( (int)VguiUtil.XRes( w ), (int)VguiUtil.YRes( t ) );
		SetImage( Tga );
	}

	public override void PaintBackground()
	{
	}
}

public class TFSlider : Slider
{
	public TFSlider( int x, int y, int wide, int tall, bool vertical ) : base( x, y, wide, tall, vertical )
	{
	}

	public override void PaintBackground()
	{
		GetPaintSize( out var wide, out var tall );
		GetNobPos( out var nobx, out var noby );

		DrawSetColor( SchemeColor.Secondary1 );
		DrawOutlinedRect( 0, 0, wide, tall );

		if ( IsVertical() )
		{
			DrawSetColor( SchemeColor.Primary2 );
			DrawFilledRect( 0, nobx, wide, noby );

			DrawSetColor( SchemeColor.Primary1 );
			DrawOutlinedRect( 0, nobx, wide, noby );
		}
		else
		{
			DrawSetColor( SchemeColor.Primary2 );
			DrawFilledRect( nobx, 0, noby, tall );

			DrawSetColor( SchemeColor.Primary1 );
			DrawOutlinedRect( nobx, 0, noby, tall );
		}
	}
}
