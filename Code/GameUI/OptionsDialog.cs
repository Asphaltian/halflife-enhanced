using System;

namespace GoldSrc.GameUI;

public sealed class OptionsSubAudio : PropertyPage
{
	private readonly CvarSlider _sfxSlider;
	private readonly CvarSlider _suitSlider;
	private readonly CvarSlider _musicSlider;
	private readonly LabeledCommandComboBox _soundQuality;

	public OptionsSubAudio( Vgui vgui, Panel parent ) : base( vgui, parent, null )
	{
		_sfxSlider = new CvarSlider( vgui, this, "SFX Slider", 0f, 2f, "volume" );
		_suitSlider = new CvarSlider( vgui, this, "Suit Slider", 0f, 2f, "suitvolume" );
		_musicSlider = new CvarSlider( vgui, this, "MP3 Volume", 0f, 1f, "mp3volume" );

		_soundQuality = new LabeledCommandComboBox( vgui, this, "Sound Quality" );
		_soundQuality.AddItem( "#GameUI_High", "hisound 1" );
		_soundQuality.AddItem( "#GameUI_Low", "hisound 0" );
		_soundQuality.SetInitialItem( Cvar.GetFloat( "hisound" ) == 0f ? 1 : 0 );

		LoadControlSettings( "Resource\\OptionsSubAudio.res" );

		FindChildByName( "MilesAudioLabel" )?.SetVisible( false );

		if ( FindChildByName( "mp3 label" ) is Label musicLabel )
			musicLabel.SetText( vgui.Localize( "#GameUI_MP3Volume" ).TrimEnd( '*', ' ' ) );

		if ( !ModInfo.IsMultiplayerOnly )
			return;

		FindChildByName( "suit label" )?.SetVisible( false );
		FindChildByName( "Suit Slider" )?.SetVisible( false );
	}

	protected override void OnResetData()
	{
		_sfxSlider.Reset();
		_suitSlider.Reset();
		_musicSlider.Reset();
		_soundQuality.Reset();
	}

	protected override void OnApplyChanges()
	{
		_sfxSlider.ApplyChanges();
		_suitSlider.ApplyChanges();
		_musicSlider.ApplyChanges();
		_soundQuality.ApplyChanges();
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "ControlModified" )
		{
			PostActionSignal( new Message( "ApplyButtonEnable" ) );
			return;
		}

		base.OnMessage( message, from );
	}
}

public sealed class OptionsSubVideo : PropertyPage
{
	private static readonly string[] UnsupportedControls = ["Renderer", "Label2", "Resolution", "Label1", "AspectRatio", "Label4", "Windowed", "WidescreenFOV", "VSync", "UseShaders", "Label5"];

	private readonly CvarSlider _brightnessSlider;
	private readonly CvarSlider _gammaSlider;
	private readonly CvarToggleCheckButton _textureFiltering;

	public OptionsSubVideo( Vgui vgui, Panel parent ) : base( vgui, parent, null )
	{
		_brightnessSlider = new CvarSlider( vgui, this, "Brightness", 0f, 2f, "brightness" );
		_gammaSlider = new CvarSlider( vgui, this, "Gamma", 1f, 3f, "gamma" );
		_textureFiltering = new CvarToggleCheckButton( vgui, this, "TextureFiltering", "#GameUI_TextureFiltering", "r_prefertexturefiltering" );

		LoadControlSettings( "Resource\\OptionsSubVideo.res" );

		foreach ( var name in UnsupportedControls )
			FindChildByName( name )?.SetVisible( false );
	}

	protected override void OnResetData()
	{
		_brightnessSlider.Reset();
		_gammaSlider.Reset();
		_textureFiltering.Reset();
	}

	protected override void OnApplyChanges()
	{
		var gammaModified = _gammaSlider.HasBeenModified();

		_brightnessSlider.ApplyChanges();
		_gammaSlider.ApplyChanges();
		_textureFiltering.ApplyChanges();

		if ( gammaModified )
			Cmd.AddText( "_restart\n" );
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "ControlModified" )
		{
			PostActionSignal( new Message( "ApplyButtonEnable" ) );
			return;
		}

		base.OnMessage( message, from );
	}
}

public sealed class OptionsSubMouse : PropertyPage
{
	private const float MinimumTypedSensitivity = 1f;
	private const int SensitivityTextLength = 63;

	private readonly CvarNegateCheckButton _reverseMouse;
	private readonly CvarToggleCheckButton _lowLatency;
	private readonly CvarToggleCheckButton _mouseFilter;
	private readonly CvarToggleCheckButton _rawInput;
	private readonly CvarToggleCheckButton _autoAim;
	private readonly CvarSlider _mouseSensitivity;
	private readonly TextEntry _sensitivityLabel;

	public OptionsSubMouse( Vgui vgui, Panel parent ) : base( vgui, parent, null )
	{
		_reverseMouse = new CvarNegateCheckButton( vgui, this, "ReverseMouse", "#GameUI_ReverseMouse", "m_pitch" );
		_lowLatency = new CvarToggleCheckButton( vgui, this, "LowLatency", "#GameUI_LowLatency", "gl_lowlatency" );
		_mouseFilter = new CvarToggleCheckButton( vgui, this, "MouseFilter", "#GameUI_MouseFilter", "m_filter" );
		_mouseSensitivity = new CvarSlider( vgui, this, "Slider", 0.2f, 20f, "sensitivity", true );
		_rawInput = new CvarToggleCheckButton( vgui, this, "RawInput", "#GameUI_RawInput", "m_rawinput" );

		_sensitivityLabel = new TextEntry( vgui, this, "SensitivityLabel" );
		_sensitivityLabel.AddActionSignalTarget( this );

		_autoAim = new CvarToggleCheckButton( vgui, this, "Auto-Aim", "#GameUI_AutoAim", "sv_aim" );

		LoadControlSettings( "Resource\\OptionsSubMouse.res" );

		_sensitivityLabel.SetText( FormatSensitivity( Cvar.GetFloat( "sensitivity" ) ) );

		_rawInput.SetVisible( false );
		_lowLatency.SetVisible( false );

		foreach ( var child in Children )
		{
			if ( child.Name == "RawInputLabel" )
				child.SetVisible( false );
		}
	}

	private static string FormatSensitivity( float value ) => " " + value.ToString( "F1", System.Globalization.CultureInfo.InvariantCulture );

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		_sensitivityLabel.SetBorder( Vgui.Scheme.GetBorder( "ButtonDepressedBorder" ) );
	}

	protected override void OnResetData()
	{
		_reverseMouse.Reset();
		_lowLatency.Reset();
		_mouseFilter.Reset();
		_mouseSensitivity.Reset();
		_rawInput.Reset();
		_autoAim.Reset();
	}

	protected override void OnApplyChanges()
	{
		_reverseMouse.ApplyChanges();
		_lowLatency.ApplyChanges();
		_mouseFilter.ApplyChanges();
		_mouseSensitivity.ApplyChanges();
		_autoAim.ApplyChanges();
		_rawInput.ApplyChanges();
	}

	private void OnControlModified( Panel panel )
	{
		PostActionSignal( new Message( "ApplyButtonEnable" ) );

		if ( panel == _mouseSensitivity && _mouseSensitivity.HasBeenModified() )
			_sensitivityLabel.SetText( FormatSensitivity( _mouseSensitivity.GetSliderValue() ) );
	}

	private void OnTextChanged( Panel panel )
	{
		if ( panel != _sensitivityLabel )
			return;

		var text = _sensitivityLabel.Text;

		if ( text.Length > SensitivityTextLength )
			text = text[..SensitivityTextLength];

		var value = (float)Common.Strtod( text );

		if ( value < MinimumTypedSensitivity )
			return;

		_mouseSensitivity.SetSliderValue( value );

		PostActionSignal( new Message( "ApplyButtonEnable" ) );
	}

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "ControlModified":
				OnControlModified( message.Panel );
				break;

			case "TextChanged":
				OnTextChanged( message.Panel );
				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}
}

public sealed class OptionsDialog : PropertyDialog
{
	public OptionsDialog( Vgui vgui, Panel parent ) : base( vgui, parent, "OptionsDialog" )
	{
		SetHdProportional( true );
		SetBounds( 0, 0, Scaled( 522 ), Scaled( 444 ) );
		SetCloseSound( "sound/UI/launch_dnmenu1.wav" );

		BasePanel.PositionDialog( this );

		SetTitle( "#GameUI_Options" );

		var halfLife = ModInfo.GameName.Equals( "Half-Life", StringComparison.OrdinalIgnoreCase );

		if ( ModInfo.IsMultiplayerOnly && !ModInfo.IsSinglePlayerOnly )
			AddPage( new OptionsSubMultiplayer( vgui, this ), "#GameUI_Multiplayer" );

		AddPage( new OptionsSubAudio( vgui, this ), "#GameUI_Audio" );
		AddPage( new OptionsSubVideo( vgui, this ), "#GameUI_Video" );
		AddPage( new OptionsSubMouse( vgui, this ), "#GameUI_Mouse" );
		AddPage( new OptionsSubKeyboard( vgui, this ), "#GameUI_Keyboard" );

		if ( !ModInfo.IsSinglePlayerOnly || halfLife )
			AddPage( new OptionsSubVoice( vgui, this ), "#GameUI_Voice" );

		AddPage( new OptionsSubAdvanced( vgui, this ), "#GameUI_Content" );

		if ( (!ModInfo.IsMultiplayerOnly && !ModInfo.IsSinglePlayerOnly) || halfLife )
			AddPage( new OptionsSubMultiplayer( vgui, this ), "#GameUI_Multiplayer" );

		SetApplyButtonVisible( true );

		PropertySheet.SetTabWidth( Scaled( 72 ) );
	}

	public override void Activate()
	{
		base.Activate();

		EnableApplyButton( false );
	}

	public override void Close()
	{
		base.Close();

		MarkForDeletion();
	}
}
