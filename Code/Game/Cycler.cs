using GoldSrc;

namespace HalfLife;

public class Cycler : BaseMonster
{
	[Save( FieldType.Integer )]
	private int _animate;

	public override ObjectCaps ObjectCaps => ObjectCaps.AcrossTransition | ObjectCaps.ImpulseUse;

	public override bool IsAlive => false;

	public override bool IsAllowedToSpeak => true;

	public void GenericCyclerSpawn( string model, Vector3 mins, Vector3 maxs )
	{
		if ( string.IsNullOrEmpty( model ) )
		{
			Util.Alert( AlertType.Error, $"cycler at {Origin.x:0} {Origin.y:0} {Origin.z:0} missing modelname" );
			Server.FreeEdict( this );
			return;
		}

		ClassName = "cycler";
		Server.PrecacheModel( model );
		SetModel( model );

		CyclerSpawn();

		SetSize( mins, maxs );
	}

	private void CyclerSpawn()
	{
		InitBoneControllers();

		Solid = Solid.SlideBox;
		MoveType = MoveType.None;
		DamageMode = DamageYes;
		Effects = 0;
		Health = 80000f;
		YawSpeed = 5f;
		IdealYaw = Angles.y;
		ChangeYaw( 360 );

		SequenceFrameRate = 75f;
		GroundSpeed = 0f;

		NextThink += 1f;

		ResetSequenceInfo();

		if ( Sequence != 0 || Frame != 0f )
		{
			_animate = 0;
			FrameRate = 0f;
		}
		else
		{
			_animate = 1;
		}
	}

	public override void Spawn() => CyclerSpawn();

	public override void Think()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( _animate != 0 )
			StudioFrameAdvance();

		if ( !SequenceFinished || SequenceLoops )
			return;

		AnimTime = Server.Globals.Time;
		FrameRate = 1f;
		SequenceFinished = false;
		LastEventCheck = Server.Globals.Time;
		Frame = 0f;

		if ( _animate == 0 )
			FrameRate = 0f;
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		_animate = _animate == 0 ? 1 : 0;

		FrameRate = _animate != 0 ? 1f : 0f;
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( _animate != 0 )
		{
			Sequence++;

			ResetSequenceInfo();

			if ( SequenceFrameRate == 0.0 )
			{
				Sequence = 0;
				ResetSequenceInfo();
			}

			Frame = 0f;
		}
		else
		{
			FrameRate = 1f;
			StudioFrameAdvance( 0.1f );
			FrameRate = 0f;
			Util.Alert( AlertType.Console, $"sequence: {Sequence}, frame {Frame:0}\n" );
		}

		return false;
	}
}

[LinkEntityToClass( "cycler" )]
public class GenericCycler : Cycler
{
	public override void Spawn() => GenericCyclerSpawn( ModelName, new Vector3( -16f, -16f, 0f ), new Vector3( 16f, 16f, 72f ) );
}

[LinkEntityToClass( "cycler_prdroid" )]
public class CyclerProbe : Cycler
{
	public override void Spawn()
	{
		Origin += new Vector3( 0f, 0f, 16f );
		GenericCyclerSpawn( "models/prdroid.mdl", new Vector3( -16f, -16f, -16f ), new Vector3( 16f, 16f, 16f ) );
	}
}

[LinkEntityToClass( "cycler_sprite" )]
public class CyclerSprite : BaseEntity
{
	[Save( FieldType.Integer )]
	private int _animate;
	[Save( FieldType.Time )]
	private float _lastTime;
	[Save( FieldType.Float )]
	private float _maxFrame;

	public override ObjectCaps ObjectCaps => base.ObjectCaps | ObjectCaps.DontSave | ObjectCaps.ImpulseUse;

	private bool ShouldAnimate => _animate != 0 && _maxFrame > 1.0;

	public override void Spawn()
	{
		Solid = Solid.SlideBox;
		MoveType = MoveType.None;
		DamageMode = DamageYes;
		Effects = 0;

		Frame = 0f;
		NextThink = (float)(Server.Globals.Time + 0.1);
		_animate = 1;
		_lastTime = Server.Globals.Time;

		Server.PrecacheModel( ModelName );
		SetModel( ModelName );

		_maxFrame = (float)Server.ModelFrames( ModelIndex ) - 1;
	}

	public override void Think()
	{
		if ( ShouldAnimate )
			Animate( FrameRate * (Server.Globals.Time - _lastTime) );

		NextThink = (float)(Server.Globals.Time + 0.1);
		_lastTime = Server.Globals.Time;
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		_animate = _animate == 0 ? 1 : 0;
		Util.Alert( AlertType.Console, $"Sprite: {ModelName}\n" );
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( _maxFrame > 1.0 )
			Animate( 1f );

		return true;
	}

	private void Animate( float frames )
	{
		Frame += frames;

		if ( _maxFrame > 0f )
			Frame %= _maxFrame;
	}
}

[LinkEntityToClass( "cycler_weapon" )]
public class WeaponCycler : BasePlayerWeapon
{
	private string _modelName;
	private int _model;

	public override int ItemSlot => 1;

	public override bool GetItemInfo( out ItemInfo info )
	{
		info = default;

		return false;
	}

	public override void Spawn()
	{
		Solid = Solid.SlideBox;
		MoveType = MoveType.None;

		Server.PrecacheModel( ModelName );
		SetModel( ModelName );
		_modelName = ModelName;
		_model = ModelIndex;

		SetOrigin( Origin );
		SetSize( new Vector3( -16f, -16f, 0f ), new Vector3( 16f, 16f, 16f ) );
		SetTouch( DefaultTouch );
	}

	public override bool Deploy()
	{
		Player.ViewModel = _modelName;
		Player.NextAttack = (float)(WeaponTimeBase + 1.0);
		SendWeaponAnim( 0 );
		Clip = 0;

		return true;
	}

	public override void Holster( int skipLocal = 0 )
	{
		Player.NextAttack = (float)(WeaponTimeBase + 0.5);
	}

	public override void PrimaryAttack()
	{
		SendWeaponAnim( Sequence );

		NextPrimaryAttack = (float)(Server.Globals.Time + 0.3);
	}

	public override void SecondaryAttack()
	{
		var frameRate = 0f;
		var groundSpeed = 0f;

		Sequence = (Sequence + 1) % 8;

		ModelIndex = _model;
		Animation.GetSequenceInfo( Server.GetModelPtr( this ), this, ref frameRate, ref groundSpeed );
		ModelIndex = 0;

		if ( frameRate == 0.0 )
			Sequence = 0;

		SendWeaponAnim( Sequence );

		NextSecondaryAttack = (float)(Server.Globals.Time + 0.3);
	}
}

[LinkEntityToClass( "cycler_wreckage" )]
public class Wreckage : BaseMonster
{
	[Save( FieldType.Time )]
	private int _startTime;

	public override void Spawn()
	{
		Solid = Solid.Not;
		MoveType = MoveType.None;
		DamageMode = DamageNo;
		Effects = 0;

		Frame = 0f;
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( !string.IsNullOrEmpty( ModelName ) )
		{
			Server.PrecacheModel( ModelName );
			SetModel( ModelName );
		}

		_startTime = (int)Server.Globals.Time;
	}

	public override void Precache()
	{
		if ( !string.IsNullOrEmpty( ModelName ) )
			Server.PrecacheModel( ModelName );
	}

	public override void Think()
	{
		StudioFrameAdvance();
		NextThink = (float)(Server.Globals.Time + 0.2);

		if ( DamageTime != 0f )
		{
			if ( DamageTime < Server.Globals.Time )
			{
				Remove();
				return;
			}

			if ( Server.RandomFloat( 0f, DamageTime - _startTime ) > DamageTime - Server.Globals.Time )
				return;
		}

		var source = new Vector3(
			Server.RandomFloat( AbsMin.x, AbsMax.x ),
			Server.RandomFloat( AbsMin.y, AbsMax.y ),
			Server.RandomFloat( AbsMin.z, AbsMax.z ) );

		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, source );
		Server.WriteByte( TempEntity.Smoke );
		Server.WriteCoord( source.x );
		Server.WriteCoord( source.y );
		Server.WriteCoord( source.z );
		Server.WriteShort( GameModels.Smoke );
		Server.WriteByte( Server.RandomLong( 0, 49 ) + 50 );
		Server.WriteByte( Server.RandomLong( 0, 3 ) + 8 );
		Server.MessageEnd();
	}
}
