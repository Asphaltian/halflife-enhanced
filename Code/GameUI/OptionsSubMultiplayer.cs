using System;

namespace GoldSrc.GameUI;

public sealed class OptionsSubMultiplayer : PropertyPage
{
	private const int MaxModelName = 64;
	private const int MaxLogoName = 32;
	private const int BitmapFileHeaderSize = 14;
	private const int BitmapInfoHeaderSize = 40;
	private const int BitmapColorsUsedOffset = 0x20;
	private const ushort BitmapMagic = 0x4d42;
	private const int PaletteEntries = 256;
	private const int TopColorStart = 0xc0;
	private const int TopColorEnd = 0xdf;
	private const int BottomColorStart = 0xa0;
	private const int BottomColorEnd = 0xbf;
	private const string LogoColorCommand = "cl_logocolor ";

	[SkipHotload]
	private static readonly (string Name, byte Red, byte Green, byte Blue)[] ItemColors =
	[
		("#Valve_Orange", 255, 120, 24),
		("#Valve_Yellow", 225, 180, 24),
		("#Valve_Blue", 0, 60, 255),
		("#Valve_Ltblue", 0, 167, 255),
		("#Valve_Green", 0, 167, 0),
		("#Valve_Red", 255, 43, 0),
		("#Valve_Brown", 123, 73, 0),
		("#Valve_Ltgray", 100, 100, 100),
		("#Valve_Dkgray", 36, 36, 36)
	];

	private readonly BitmapImagePanel _modelImage;
	private readonly LabeledCommandComboBox _modelList;
	private readonly BitmapImagePanel _logoImage;
	private readonly LabeledCommandComboBox _logoList;
	private readonly LabeledCommandComboBox _colorList;
	private readonly CvarTextEntry _nameTextEntry;
	private readonly CvarSlider _primaryColorSlider;
	private readonly CvarSlider _secondaryColorSlider;
	private readonly CvarToggleCheckButton _highQualityModelCheckBox;

	private string _logoName = "";
	private int _topColor = 30;
	private int _bottomColor = 6;
	private MultiplayerAdvancedDialog _advancedDialog;

	public OptionsSubMultiplayer( Vgui vgui, Panel parent ) : base( vgui, parent, "OptionsSubMultiplayer" )
	{
		new Button( vgui, this, "Advanced", "#GameUI_AdvancedEllipsis" ).SetCommand( "Advanced" );
		new Label( vgui, this, "NameLabel", "#GameUI_PlayerName" );

		_nameTextEntry = new CvarTextEntry( vgui, this, "NameEntry", "name" );
		_nameTextEntry.SetCatchEnterKey( true );

		_primaryColorSlider = new CvarSlider( vgui, this, "Primary Color Slider", 0f, 255f, "topcolor" );
		_secondaryColorSlider = new CvarSlider( vgui, this, "Secondary Color Slider", 0f, 255f, "bottomcolor" );
		_highQualityModelCheckBox = new CvarToggleCheckButton( vgui, this, "High Quality Models", "#GameUI_HighModels", "cl_himodels" );

		_modelList = new LabeledCommandComboBox( vgui, this, "Player model" );
		InitModelList( _modelList );

		_logoList = new LabeledCommandComboBox( vgui, this, "SpraypaintList" );
		InitLogoList( _logoList );

		_colorList = new LabeledCommandComboBox( vgui, this, "SpraypaintColor" );

		var currentColor = Cvar.GetString( "cl_logocolor" );
		var selected = 0;

		for ( var i = 0; i < ItemColors.Length; i++ )
		{
			if ( currentColor is not null && currentColor.Equals( ItemColors[i].Name, StringComparison.OrdinalIgnoreCase ) )
				selected = i;

			_colorList.AddItem( ItemColors[i].Name, $"{LogoColorCommand}{ItemColors[i].Name}\n" );
		}

		_colorList.SetInitialItem( selected );
		_colorList.AddActionSignalTarget( this );

		_modelImage = new BitmapImagePanel( vgui, this, "ModelImage" );
		_modelImage.AddActionSignalTarget( this );

		_logoImage = new BitmapImagePanel( vgui, this, "LogoImage" );
		_logoImage.AddActionSignalTarget( this );

		var ok = new Button( vgui, this, "OK", "#GameUI_OK" );
		var apply = new Button( vgui, this, "Apply", "#GameUI_Apply" );
		var cancel = new Button( vgui, this, "Cancel", "#GameUI_Cancel" );

		LoadControlSettings( "Resource/OptionsSubMultiplayer.res" );

		ok.SetEnabled( false );
		ok.SetVisible( false );
		apply.SetEnabled( false );
		apply.SetVisible( false );
		cancel.SetEnabled( false );
		cancel.SetVisible( false );

		if ( ModInfo.NoModels )
		{
			_modelImage.SetVisible( false );
			_modelList.SetVisible( false );
			_primaryColorSlider.SetVisible( false );
			_secondaryColorSlider.SetVisible( false );

			FindChildByName( "Label1" )?.SetVisible( false );
			FindChildByName( "Colors" )?.SetVisible( false );
		}

		if ( ModInfo.NoHiModels )
			_highQualityModelCheckBox.SetVisible( false );
	}

	private static void InitModelList( LabeledCommandComboBox list )
	{
		Server.RemoveFile( "models/player/remapped.bmp" );

		var currentModel = Cvar.GetString( "model" ) ?? "";

		if ( currentModel.Length >= MaxModelName )
			currentModel = currentModel[..(MaxModelName - 1)];

		var count = 0;
		var initialItem = 0;

		foreach ( var name in Server.FindDirectories( "models/player" ) )
		{
			if ( name[0] == '.' || list.IsItemPresent( name ) )
				continue;

			if ( currentModel.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				initialItem = count;

			list.AddItem( name, $"model {name}\n" );

			count++;
		}

		list.SetInitialItem( initialItem );
	}

	private static void InitLogoList( LabeledCommandComboBox list )
	{
		Server.RemoveFile( "logos/remapped.bmp" );

		var currentLogo = Cvar.GetString( "cl_logofile" ) ?? "";
		var names = new SortedSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var fileName in Server.FindFiles( "logos", ".bmp" ) )
		{
			if ( fileName.Equals( "remapped.bmp", StringComparison.OrdinalIgnoreCase ) || fileName[0] == '.' )
				continue;

			names.Add( fileName.Length > 3 ? fileName[..^4] : fileName );
		}

		var count = 0;

		foreach ( var name in names )
		{
			list.AddItem( name, "" );

			if ( name.Equals( currentLogo, StringComparison.OrdinalIgnoreCase ) )
				list.SetInitialItem( count );

			count++;
		}
	}

	private static (byte Red, byte Green, byte Blue) ColorForName( string name )
	{
		foreach ( var color in ItemColors )
		{
			if ( name.StartsWith( color.Name, StringComparison.OrdinalIgnoreCase ) )
				return (color.Red, color.Green, color.Blue);
		}

		return (0, 0, 0);
	}

	private static void PaletteHueReplace( byte[] bitmap, int palette, int newHue, int start, int end )
	{
		var hue = (float)(newHue * (360.0 / 255));

		for ( var i = start; i <= end; i++ )
		{
			var entry = palette + (i * 4);

			float b = bitmap[entry];
			float g = bitmap[entry + 1];
			float r = bitmap[entry + 2];

			var maxColor = MathF.Max( MathF.Max( r, g ), b ) / 255f;
			var minColor = MathF.Min( MathF.Min( r, g ), b ) / 255f;

			var value = maxColor;
			var saturation = (maxColor - minColor) / maxColor;

			minColor = (1f - saturation) * value;

			var range = value - minColor;

			if ( hue <= 120f )
			{
				b = minColor;

				if ( hue < 60f )
				{
					r = value;
					g = (range * hue / (120f - hue)) + minColor;
				}
				else
				{
					g = value;
					r = ((120f - hue) * range / hue) + minColor;
				}
			}
			else if ( hue <= 240f )
			{
				r = minColor;

				if ( hue < 180f )
				{
					g = value;
					b = ((hue - 120f) * range / (240f - hue)) + minColor;
				}
				else
				{
					b = value;
					g = ((240f - hue) * range / (hue - 120f)) + minColor;
				}
			}
			else
			{
				g = minColor;

				if ( hue < 300f )
				{
					b = value;
					r = ((hue - 240f) * range / (360f - hue)) + minColor;
				}
				else
				{
					r = value;
					b = ((360f - hue) * range / (hue - 240f)) + minColor;
				}
			}

			bitmap[entry] = (byte)(int)(b * 255f);
			bitmap[entry + 1] = (byte)(int)(g * 255f);
			bitmap[entry + 2] = (byte)(int)(r * 255f);
		}
	}

	private static bool IsBitmap( byte[] data ) => data.Length >= BitmapFileHeaderSize && BitConverter.ToUInt16( data, 0 ) == BitmapMagic;

	private static void WriteRemapped( string fileName, byte[] data )
	{
		Server.RemoveFile( fileName );

		Server.WriteFile( fileName, IsBitmap( data ) ? data : data[..Math.Min( data.Length, BitmapFileHeaderSize )] );
	}

	private static void RemapPalette( string fileName, int topColor, int bottomColor )
	{
		if ( Server.ReadFile( $"models/player/{fileName}/{fileName}.bmp" ) is not { } data )
			return;

		if ( IsBitmap( data ) )
		{
			var palette = BitmapFileHeaderSize + BitmapInfoHeaderSize;

			if ( topColor < TopColorEnd )
				PaletteHueReplace( data, palette, topColor, TopColorStart, TopColorEnd );

			if ( bottomColor < BottomColorEnd )
				PaletteHueReplace( data, palette, bottomColor, BottomColorStart, BottomColorEnd );
		}

		WriteRemapped( "models/player/remapped.bmp", data );
	}

	private static void RemapLogoPalette( string fileName, int red, int green, int blue )
	{
		if ( Server.ReadFile( $"logos/{fileName}.bmp" ) is not { } data )
			return;

		if ( IsBitmap( data ) )
		{
			BitConverter.TryWriteBytes( data.AsSpan( BitmapFileHeaderSize + BitmapColorsUsedOffset ), PaletteEntries );

			var palette = BitmapFileHeaderSize + BitmapInfoHeaderSize;
			var index = 0f;

			for ( var i = 0; i < PaletteEntries; i++ )
			{
				var fraction = index / PaletteEntries;

				data[palette + (i * 4) + 2] = (byte)(int)(red * fraction);
				data[palette + (i * 4) + 1] = (byte)(int)(green * fraction);
				data[palette + (i * 4)] = (byte)(int)(blue * fraction);

				index += 1f;
			}
		}

		WriteRemapped( "logos/remapped.bmp", data );
	}

	private void RemapModel()
	{
		var modelName = _modelList.Text;

		if ( modelName.Length == 0 )
			return;

		RemapPalette( modelName, _topColor, _bottomColor );

		_modelImage.SetTexture( "models/player/remapped" );
	}

	private void OnTextChanged( Panel panel )
	{
		if ( panel == _nameTextEntry )
			return;

		RemapModel();

		var logoName = _logoList.Text;

		if ( logoName.Length == 0 )
			return;

		string texture;
		bool remapped;

		if ( Server.FindMountPath( $"logos/{logoName}.tga", ".vtex" ) is not null )
		{
			texture = $"logos/{logoName}";
			remapped = false;
		}
		else
		{
			texture = "logos/remapped";

			var command = _colorList.GetActiveItemCommand();

			if ( string.IsNullOrEmpty( command ) )
				return;

			var (red, green, blue) = ColorForName( command[LogoColorCommand.Length..] );

			RemapLogoPalette( logoName, red, green, blue );

			remapped = true;
		}

		_colorList.SetEnabled( remapped );
		_logoImage.SetTexture( texture );
	}

	private void OnSliderMoved()
	{
		_topColor = (int)_primaryColorSlider.GetSliderValue();
		_bottomColor = (int)_secondaryColorSlider.GetSliderValue();

		RemapModel();
	}

	private void OnControlModified()
	{
		PostActionSignal( new Message( "ApplyButtonEnable" ) );
		InvalidateLayout();
	}

	protected override void OnApplyChanges()
	{
		_primaryColorSlider.ApplyChanges();
		_secondaryColorSlider.ApplyChanges();
		_modelList.ApplyChanges();
		_logoList.ApplyChanges();

		_logoName = _logoList.Text;

		if ( _logoName.Length >= MaxLogoName )
			_logoName = _logoName[..(MaxLogoName - 1)];

		_colorList.ApplyChanges();
		_highQualityModelCheckBox.ApplyChanges();
		_nameTextEntry.ApplyChanges();

		var command = _colorList.GetActiveItemCommand();

		if ( string.IsNullOrEmpty( command ) )
			return;

		Client.ClientCmd( $"cl_logofile {_logoName}\n" );

		if ( Server.FindMountPath( $"logos/{_logoName}.tga", ".vtex" ) is not null )
			return;

		var (red, green, blue) = ColorForName( command[LogoColorCommand.Length..] );

		if ( Server.ReadFile( "logos/remapped.bmp" ) is { } data && IsBitmap( data ) )
			LogoFile.UpdateLogoWad( data[BitmapFileHeaderSize..], red, green, blue );
	}

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "TextChanged":
				OnTextChanged( message.Panel );
				break;

			case "SliderMoved":
				OnSliderMoved();
				break;

			case "ControlModified":
				OnControlModified();
				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}

	public override void OnCommand( string command )
	{
		if ( command.Equals( "Advanced", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( _advancedDialog is null or { Deleted: true } )
				_advancedDialog = new MultiplayerAdvancedDialog( Vgui, this );

			_advancedDialog.Activate();
		}
		else if ( command.Equals( "ShowKeyboard", StringComparison.OrdinalIgnoreCase ) )
		{
			Client.ClientCmd( "show_osk 609 273" );
		}

		base.OnCommand( command );
	}
}
