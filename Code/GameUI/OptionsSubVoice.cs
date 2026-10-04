using System;

namespace GoldSrc.GameUI;

public sealed class OptionsSubVoice : PropertyPage
{
	private const int MeterWide = 160;
	private const int MeterVolumeShift = 15;
	private const int MeterVolumeRound = 0x7fff;

	private readonly ImagePanel _micMeter;
	private readonly ImagePanel _micMeter2;
	private readonly Button _testMicrophoneButton;
	private readonly Label _microphoneSliderLabel;
	private readonly Slider _micVolume;
	private readonly Label _receiveSliderLabel;
	private readonly CvarSlider _receiveVolume;
	private readonly CvarToggleCheckButton _voiceEnableCheckButton;
	private readonly CheckButton _micBoost;

	private int _micVolumeValue;
	private bool _micBoostSelected;
	private float _receiveVolumeValue;
	private int _receiveSliderValue;
	private bool _voiceOn;

	public OptionsSubVoice( Vgui vgui, Panel parent ) : base( vgui, parent, null )
	{
		_micMeter = new ImagePanel( vgui, this, "MicMeter" );
		_micMeter2 = new ImagePanel( vgui, this, "MicMeter2" );

		_receiveSliderLabel = new Label( vgui, this, "ReceiveLabel", "#GameUI_VoiceReceiveVolume" );
		_receiveVolume = new CvarSlider( vgui, this, "VoiceReceive", 0f, 1f, "voice_scale" );

		_microphoneSliderLabel = new Label( vgui, this, "MicrophoneLabel", "#GameUI_VoiceTransmitVolume" );

		_micVolume = new Slider( vgui, this, "VoiceVolume" );
		_micVolume.SetRange( 0, 100 );
		_micVolume.AddActionSignalTarget( this );

		_voiceEnableCheckButton = new CvarToggleCheckButton( vgui, this, "voice_modenable", "#GameUI_EnableVoice", "voice_modenable" );

		_micBoost = new CheckButton( vgui, this, "MicBoost", "#GameUI_BoostMicrophone" );
		_micBoost.AddActionSignalTarget( this );

		_testMicrophoneButton = new Button( vgui, this, "TestMicrophone", "#GameUI_TestMicrophone" );

		LoadControlSettings( "Resource\\OptionsSubVoice.res" );

		if ( _micMeter2.Image is { } image )
		{
			var (wide, tall) = image.GetSize();

			image.SetSize( Scaled( wide ), Scaled( tall ) );
		}

		_voiceOn = false;

		_micMeter2.SetVisible( false );

		_micVolume.SetVisible( false );
		_micBoost.SetVisible( false );

		FindChildByName( "Transmit label" )?.SetVisible( false );
		FindChildByName( "MilesVoiceLabel" )?.SetVisible( false );

		if ( FindChildByName( "Label1" ) is Label receiveLabel )
			receiveLabel.SetText( vgui.Localize( "#GameUI_VoiceReceiveVolume" ).TrimEnd( '*', ' ' ) );

		ReadVoiceParameters();
	}

	private static Client Tweak => Client.Current;

	private void ReadVoiceParameters()
	{
		_micVolume.SetValue( (int)(Tweak.GetVoiceTweakControl( VoiceTweakControl.MicrophoneVolume ) * 100f) );
		_micVolumeValue = _micVolume.Value;

		_micBoost.SetSelected( Tweak.GetVoiceTweakControl( VoiceTweakControl.MicBoost ) != 0f );
		_micBoostSelected = _micBoost.IsSelected;

		_receiveVolume.Reset();
		_receiveVolumeValue = _receiveVolume.GetSliderValue();

		_voiceEnableCheckButton.Reset();
	}

	protected override void OnResetData() => ReadVoiceParameters();

	protected override void OnApplyChanges()
	{
		_micVolumeValue = _micVolume.Value;

		Tweak.SetVoiceTweakControl( VoiceTweakControl.MicrophoneVolume, _micVolumeValue / 100f );

		_micBoostSelected = _micBoost.IsSelected;

		Tweak.SetVoiceTweakControl( VoiceTweakControl.MicBoost, _micBoostSelected ? 1f : 0f );

		_receiveVolume.ApplyChanges();
		_receiveVolumeValue = _receiveVolume.GetSliderValue();

		_voiceEnableCheckButton.ApplyChanges();
	}

	private void UseCurrentVoiceParameters()
	{
		Tweak.SetVoiceTweakControl( VoiceTweakControl.MicrophoneVolume, _micVolumeValue / 100f );
		Tweak.SetVoiceTweakControl( VoiceTweakControl.MicBoost, _micBoostSelected ? 1f : 0f );

		Cvar.Set( "voice_scale", _receiveVolumeValue.ToString( System.Globalization.CultureInfo.InvariantCulture ) );

		_receiveVolume.Reset();
		_receiveVolume.SetValue( _receiveSliderValue );
	}

	private void SetTestControlsEnabled( bool state )
	{
		_receiveVolume.SetEnabled( state );
		_micVolume.SetEnabled( state );
		_voiceEnableCheckButton.SetEnabled( state );
		_micBoost.SetEnabled( state );
		_microphoneSliderLabel.SetEnabled( state );
		_receiveSliderLabel.SetEnabled( state );
	}

	private void StartTestMicrophone()
	{
		_voiceOn = true;

		Tweak.SetVoiceTweakControl( VoiceTweakControl.MicrophoneVolume, _micVolume.Value / 100f );
		Tweak.SetVoiceTweakControl( VoiceTweakControl.MicBoost, _micBoost.IsSelected ? 1f : 0f );

		_receiveSliderValue = _receiveVolume.Value;

		_receiveVolume.ApplyChanges();

		if ( !Tweak.StartVoiceTweakMode() )
		{
			UseCurrentVoiceParameters();

			_voiceOn = false;

			return;
		}

		_testMicrophoneButton.SetText( "#GameUI_StopTestMicrophone" );

		SetTestControlsEnabled( false );

		_micMeter2.SetVisible( true );
	}

	private void EndTestMicrophone()
	{
		if ( !_voiceOn )
			return;

		Tweak.EndVoiceTweakMode();

		UseCurrentVoiceParameters();

		_testMicrophoneButton.SetText( "#GameUI_TestMicrophone" );

		_voiceOn = false;

		SetTestControlsEnabled( true );

		_micMeter2.SetVisible( false );
	}

	public override void OnCommand( string command )
	{
		if ( !command.Equals( "TestMicrophone", StringComparison.OrdinalIgnoreCase ) )
		{
			base.OnCommand( command );
			return;
		}

		if ( _voiceOn )
			EndTestMicrophone();
		else
			StartTestMicrophone();
	}

	protected override void OnPageHide()
	{
		if ( _voiceOn )
			EndTestMicrophone();

		base.OnPageHide();
	}

	public override void OnThink()
	{
		base.OnThink();

		if ( !_voiceOn )
			return;

		var scaled = Tweak.VoiceTweakSpeakingVolume * MeterWide;
		var wide = Scaled( (scaled + ((scaled >> 31) & MeterVolumeRound)) >> MeterVolumeShift );
		var step = Scaled( 8 );

		wide += step - Scaled( 1 );

		_micMeter2.SetSize( wide - (wide % step), _micMeter2.Tall );
	}

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "SliderMoved":
				if ( _micVolume.Value != _micVolumeValue )
					PostActionSignal( new Message( "ApplyButtonEnable" ) );

				break;

			case "CheckButtonChecked":
				if ( _micBoost.IsSelected != _micBoostSelected )
					PostActionSignal( new Message( "ApplyButtonEnable" ) );

				break;

			case "ControlModified":
				PostActionSignal( new Message( "ApplyButtonEnable" ) );
				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}
}
