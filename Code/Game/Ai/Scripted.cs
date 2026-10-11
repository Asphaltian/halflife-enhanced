using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "scripted_sequence" )]
public class CineMonster : BaseMonster
{
	public const int SpawnFlagRepeatable = 4;
	public const int SpawnFlagLeaveCorpse = 8;
	public const int SpawnFlagNoInterrupt = 32;
	public const int SpawnFlagOverrideState = 64;
	public const int SpawnFlagNoScriptMovement = 128;

	public const int BreakConditions = ConditionLightDamage | ConditionHeavyDamage;

	public const int InterruptIdle = 0;
	public const int InterruptByName = 1;
	public const int InterruptAi = 2;

	protected const int FinishScheduleDefault = 0;
	protected const int FinishScheduleAmbush = 1;

	private const string ScriptedSequenceClassName = "scripted_sequence";

	[Save( FieldType.String )]
	public string Idle;
	[Save( FieldType.String )]
	public string Play;
	[Save( FieldType.String )]
	public string Entity;
	[Save( FieldType.Integer )]
	public int MoveTo;
	[Save( FieldType.Float )]
	public float Repeat;
	[Save( FieldType.Float )]
	public float Radius;
	[Save( FieldType.Integer )]
	public int DelayCount;
	[Save( FieldType.Time )]
	public float StartTime;
	[Save( FieldType.Integer )]
	public MoveType SavedMoveType;
	[Save( FieldType.Integer )]
	public Solid SavedSolid;
	[Save( FieldType.Integer )]
	public Effects SavedEffects;
	[Save( FieldType.Integer )]
	public int FinishSchedule;
	[Save( FieldType.Boolean )]
	public bool Interruptable;

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "m_iszIdle":
				Idle = Server.AllocString( value );
				return true;
			case "m_iszPlay":
				Play = Server.AllocString( value );
				return true;
			case "m_iszEntity":
				Entity = Server.AllocString( value );
				return true;
			case "m_fMoveTo":
				MoveTo = Util.Atoi( value );
				return true;
			case "m_flRepeat":
				Repeat = Util.Atof( value );
				return true;
			case "m_flRadius":
				Radius = Util.Atof( value );
				return true;
			case "m_iFinishSchedule":
				FinishSchedule = Util.Atoi( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		Solid = Solid.Not;

		if ( string.IsNullOrEmpty( TargetName ) || !string.IsNullOrEmpty( Idle ) )
		{
			SetThink( CineThink );
			NextThink = (float)(Server.Globals.Time + 1.0);

			if ( !string.IsNullOrEmpty( TargetName ) )
				StartTime = (float)(Server.Globals.Time + 1E6);
		}

		Interruptable = (SpawnFlags & SpawnFlagNoInterrupt) == 0;
	}

	public virtual bool CanOverrideState => (SpawnFlags & SpawnFlagOverrideState) != 0;

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( TargetEntity is BaseMonster target )
		{
			if ( target.ScriptState == ScriptState.Playing )
				return;

			StartTime = (float)(Server.Globals.Time + 0.05);
		}
		else
		{
			SetThink( CineThink );
			NextThink = (float)Server.Globals.Time;
		}
	}

	public override void Blocked( BaseEntity other )
	{
	}

	public override void Touch( BaseEntity other )
	{
	}

	public void Die() => SetThink( SubRemove );

	public bool FindEntity()
	{
		var target = Util.FindEntityByTargetname( null, Entity );

		TargetEntity = null;

		while ( target is not null )
		{
			if ( (target.EntityFlags & EntityFlags.Monster) == EntityFlags.Monster )
			{
				if ( target is BaseMonster monster && monster.CanPlaySequence( CanOverrideState, InterruptByName ) )
				{
					TargetEntity = monster;
					return true;
				}

				Util.Alert( AlertType.Console, $"Found {Entity}, but can't play!\n" );
			}

			target = Util.FindEntityByTargetname( target, Entity );
		}

		BaseEntity entity = null;

		while ( (entity = Util.FindEntityInSphere( entity, Origin, Radius )) is not null )
		{
			if ( entity.ClassName != Entity || (entity.EntityFlags & EntityFlags.Monster) != EntityFlags.Monster )
				continue;

			if ( entity is BaseMonster monster && monster.CanPlaySequence( CanOverrideState, InterruptIdle ) )
			{
				TargetEntity = monster;
				return true;
			}
		}

		TargetEntity = null;

		return false;
	}

	protected void Possess( BaseMonster target )
	{
		target.GoalEntity = this;
		target.Cine = this;
		target.TargetEntity = this;

		SavedMoveType = target.MoveType;
		SavedSolid = target.Solid;
		SavedEffects = target.Effects;

		target.Effects |= Effects;
	}

	protected void TeleportToScript( BaseMonster target )
	{
		Server.SetOrigin( target, Origin );

		target.IdealYaw = Angles.y;
		target.AVelocity = Vector3.Zero;
		target.Velocity = Vector3.Zero;
		target.Effects |= Effects.NoInterp;
		target.Angles = target.Angles with { y = Angles.y };
		target.ScriptState = ScriptState.Wait;

		StartTime = (float)(Server.Globals.Time + 1E6);
	}

	public virtual void PossessEntity()
	{
		if ( TargetEntity is not BaseMonster target )
			return;

		Possess( target );

		switch ( MoveTo )
		{
			case 0:
				target.ScriptState = ScriptState.Wait;
				break;
			case 1:
				target.ScriptState = ScriptState.WalkToMark;
				DelayStart( 1 );
				break;
			case 2:
				target.ScriptState = ScriptState.RunToMark;
				DelayStart( 1 );
				break;
			case 4:
				TeleportToScript( target );
				break;
		}

		target.IdealMonsterState = MonsterState.Script;

		if ( string.IsNullOrEmpty( Idle ) )
			return;

		StartSequence( target, Idle, false );

		if ( Idle == Play )
			target.FrameRate = 0f;
	}

	public void CineThink()
	{
		if ( FindEntity() )
		{
			PossessEntity();
			Util.Alert( AlertType.AiConsole, $"script \"{TargetName}\" using monster \"{Entity}\"\n" );
		}
		else
		{
			CancelScript();
			Util.Alert( AlertType.AiConsole, $"script \"{TargetName}\" can't find monster \"{Entity}\"\n" );
			NextThink = (float)(Server.Globals.Time + 1.0);
		}
	}

	protected void SetSequence( BaseMonster target, string sequence, string kind )
	{
		target.Sequence = target.LookupSequence( sequence );

		if ( target.Sequence == -1 )
		{
			Util.Alert( AlertType.Error, $"{target.TargetName}: unknown {kind} sequence \"{sequence}\"\n" );
			target.Sequence = 0;
		}

		target.Frame = 0f;
		target.ResetSequenceInfo();
	}

	public virtual bool StartSequence( BaseMonster target, string sequence, bool completeOnEmpty )
	{
		if ( string.IsNullOrEmpty( sequence ) && completeOnEmpty )
		{
			SequenceDone( target );
			return false;
		}

		SetSequence( target, sequence, "scripted" );

		return true;
	}

	public void SequenceDone( BaseMonster monster )
	{
		if ( (SpawnFlags & SpawnFlagRepeatable) == 0 )
		{
			SetThink( SubRemove );
			NextThink = (float)(Server.Globals.Time + 0.1);
		}

		monster.CineCleanup();

		FixScriptMonsterSchedule( monster );

		UseTargets( null, UseType.Toggle, 0f );
	}

	public virtual void FixScriptMonsterSchedule( BaseMonster monster )
	{
		if ( monster.IdealMonsterState != MonsterState.Dead )
			monster.IdealMonsterState = MonsterState.Idle;

		monster.ClearSchedule();
	}

	public void AllowInterrupt( bool allow )
	{
		if ( (SpawnFlags & SpawnFlagNoInterrupt) != 0 )
			return;

		Interruptable = allow;
	}

	public bool CanInterrupt => Interruptable && TargetEntity is { DeadFlag: DeadNo };

	public override int IgnoreConditions() => CanInterrupt ? 0 : BreakConditions;

	private static void ScriptEntityCancel( BaseEntity cine )
	{
		if ( cine.ClassName != ScriptedSequenceClassName )
			return;

		if ( ((CineMonster)cine).TargetEntity is not BaseMonster target || target.MonsterState != MonsterState.Script )
			return;

		target.ScriptState = ScriptState.Cleanup;
		target.CineCleanup();
	}

	public void CancelScript()
	{
		Util.Alert( AlertType.AiConsole, $"Cancelling script: {Play}\n" );

		if ( string.IsNullOrEmpty( TargetName ) )
		{
			ScriptEntityCancel( this );
			return;
		}

		var cine = Util.FindEntityByTargetname( null, TargetName );

		while ( cine is not null )
		{
			ScriptEntityCancel( cine );
			cine = Util.FindEntityByTargetname( cine, TargetName );
		}
	}

	public void DelayStart( int state )
	{
		var entity = Util.FindEntityByTargetname( null, TargetName );

		while ( entity is not null )
		{
			if ( entity.ClassName == "scripted_sequence" )
			{
				var cine = (CineMonster)entity;

				if ( state != 0 )
				{
					cine.DelayCount++;
				}
				else
				{
					cine.DelayCount--;

					if ( cine.DelayCount <= 0 )
						cine.StartTime = (float)(Server.Globals.Time + 0.05);
				}
			}

			entity = Util.FindEntityByTargetname( entity, TargetName );
		}
	}

	public override void Activate()
	{
		var entity = Util.FindEntityByTargetname( null, Entity );
		BaseMonster target = null;

		while ( target is null && entity is not null )
		{
			if ( (entity.EntityFlags & EntityFlags.Monster) == EntityFlags.Monster )
				target = entity as BaseMonster;

			entity = Util.FindEntityByTargetname( entity, Entity );
		}

		if ( target is null )
		{
			entity = Util.FindEntityByClassname( null, Entity );

			while ( target is null && entity is not null )
			{
				target = entity as BaseMonster;
				entity = Util.FindEntityByTargetname( entity, Entity );
			}
		}

		if ( target is null || Server.GetModelPtr( target ) is not { } model )
			return;

		Animation.SequencePrecache( Server, model, Idle );
		Animation.SequencePrecache( Server, model, Play );
	}
}

[LinkEntityToClass( "aiscripted_sequence" )]
public class CineAi : CineMonster
{
	public override bool CanOverrideState => true;

	public override void PossessEntity()
	{
		if ( TargetEntity is not BaseMonster target )
			return;

		if ( !target.CanPlaySequence( CanOverrideState, InterruptAi ) )
		{
			Util.Alert( AlertType.AiConsole, $"(AI)Can't possess entity {target.ClassName}\n" );
			return;
		}

		Possess( target );

		switch ( MoveTo )
		{
			case 0:
			case 5:
				target.ScriptState = ScriptState.Wait;
				break;
			case 1:
				target.ScriptState = ScriptState.WalkToMark;
				break;
			case 2:
				target.ScriptState = ScriptState.RunToMark;
				break;
			case 4:
				TeleportToScript( target );
				target.EntityFlags &= ~EntityFlags.OnGround;
				break;
			default:
				Util.Alert( AlertType.AiConsole, "aiscript:  invalid Move To Position value!" );
				break;
		}

		Util.Alert( AlertType.AiConsole, $"\"{target.TargetName}\" found and used\n" );

		target.IdealMonsterState = MonsterState.Script;

		if ( target.MonsterState == MonsterState.Script )
			target.ChangeSchedule( target.GetScheduleOfType( ScheduleType.AiScript ) );
	}

	public override bool StartSequence( BaseMonster target, string sequence, bool completeOnEmpty )
	{
		if ( string.IsNullOrEmpty( sequence ) && completeOnEmpty )
		{
			SequenceDone( target );
			return true;
		}

		SetSequence( target, sequence, "aiscripted" );

		return true;
	}

	public override void FixScriptMonsterSchedule( BaseMonster monster )
	{
		switch ( FinishSchedule )
		{
			case FinishScheduleDefault:
				monster.ClearSchedule();
				break;
			case FinishScheduleAmbush:
				monster.ChangeSchedule( monster.GetScheduleOfType( ScheduleType.Ambush ) );
				break;
			default:
				Util.Alert( AlertType.AiConsole, "FixScriptMonsterSchedule - no case!\n" );
				monster.ClearSchedule();
				break;
		}
	}
}

partial class BaseMonster
{
	public bool ExitScriptedSequence()
	{
		if ( DeadFlag == DeadDying )
		{
			IdealMonsterState = MonsterState.Dead;
			return false;
		}

		Cine?.CancelScript();

		return true;
	}

	public bool CineCleanup()
	{
		var oldCine = Cine;

		if ( Cine is not null )
		{
			Cine.TargetEntity = null;
			MoveType = Cine.SavedMoveType;
			Solid = Cine.SavedSolid;
			Effects = Cine.SavedEffects;
		}
		else
		{
			MoveType = MoveType.Step;
			Solid = Solid.SlideBox;
		}

		Cine = null;
		TargetEntity = null;
		GoalEntity = null;

		if ( DeadFlag == DeadDying )
		{
			Health = 0f;
			FrameRate = 0f;
			Solid = Solid.Not;
			SetState( MonsterState.Dead );
			DeadFlag = DeadDead;
			SetSize( Mins, new Vector3( Maxs.x, Maxs.y, Mins.z + 2f ) );

			if ( oldCine is not null && (oldCine.SpawnFlags & CineMonster.SpawnFlagLeaveCorpse) != 0 )
			{
				SetUse( null );
				SetThink( null );
				SetTouch( null );
			}
			else
			{
				SubStartFadeOut();
			}

			StopAnimation();
			MoveType = MoveType.None;
			Effects |= Effects.NoInterp;

			return false;
		}

		if ( oldCine is not null && !string.IsNullOrEmpty( oldCine.Play ) )
		{
			if ( (oldCine.SpawnFlags & CineMonster.SpawnFlagNoScriptMovement) == 0 )
			{
				var newOrigin = Server.GetBonePosition( this, 0 );
				var oldOrigin = Origin;

				if ( MathLib.Length2D( oldOrigin - newOrigin ) < 8.0 )
					newOrigin = oldOrigin;

				Origin = new Vector3( newOrigin.x, newOrigin.y, Origin.z + 1f );
				EntityFlags |= EntityFlags.OnGround;

				var drop = Server.DropToFloor( this );

				if ( drop < 0 )
				{
					Origin = oldOrigin;
				}
				else if ( drop == 0 )
				{
					Origin = Origin with { z = newOrigin.z };
					EntityFlags &= ~EntityFlags.OnGround;
				}

				SetOrigin( Origin );
				Effects |= Effects.NoInterp;
			}

			Activity = Activity.Reset;
		}

		Enemy = null;

		if ( Health > 0f )
		{
			IdealMonsterState = MonsterState.Idle;
		}
		else
		{
			IdealMonsterState = MonsterState.Dead;
			SetConditions( ConditionLightDamage );
			DeadFlag = DeadDying;
			CheckAiTrigger();
			DeadFlag = DeadNo;
		}

		SpawnFlags &= ~SpawnFlagWaitForScript;

		return true;
	}
}

[LinkEntityToClass( "scripted_sentence" )]
public class ScriptedSentence : BaseToggle
{
	private const int SpawnFlagOnce = 0x0001;
	private const int SpawnFlagFollowers = 0x0002;
	private const int SpawnFlagInterrupt = 0x0004;
	private const int SpawnFlagConcurrent = 0x0008;

	[Save( FieldType.String )]
	private string _sentence;
	[Save( FieldType.String )]
	private string _entity;
	[Save( FieldType.Float )]
	private float _radius;
	[Save( FieldType.Float )]
	private float _duration;
	[Save( FieldType.Float )]
	private float _repeat;
	[Save( FieldType.Float )]
	private float _attenuation;
	[Save( FieldType.Float )]
	private float _volume;
	[Save( FieldType.Boolean )]
	private bool _active;
	[Save( FieldType.String )]
	private string _listener;

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "sentence":
				_sentence = Server.AllocString( value );
				return true;
			case "entity":
				_entity = Server.AllocString( value );
				return true;
			case "duration":
				_duration = Util.Atof( value );
				return true;
			case "radius":
				_radius = Util.Atof( value );
				return true;
			case "refire":
				_repeat = Util.Atof( value );
				return true;
			case "attenuation":
				Impulse = Util.Atoi( value );
				return true;
			case "volume":
				_volume = (float)(Util.Atof( value ) * 0.1);
				return true;
			case "listener":
				_listener = Server.AllocString( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !_active )
			return;

		SetThink( FindThink );
		NextThink = (float)Server.Globals.Time;
	}

	public override void Spawn()
	{
		Solid = Solid.Not;

		_active = true;

		if ( string.IsNullOrEmpty( TargetName ) )
		{
			SetThink( FindThink );
			NextThink = (float)(Server.Globals.Time + 1.0);
		}

		_attenuation = Impulse switch
		{
			1 => Server.AttenuationStatic,
			2 => Server.AttenuationNormal,
			3 => Server.AttenuationNone,
			_ => Server.AttenuationIdle
		};

		Impulse = 0;

		if ( _volume <= 0f )
			_volume = 1f;
	}

	public void FindThink()
	{
		if ( FindEntity() is { } speaker )
		{
			StartSentence( speaker );

			if ( (SpawnFlags & SpawnFlagOnce) != 0 )
				Remove();

			SetThink( DelayThink );
			NextThink = (float)(Server.Globals.Time + _duration + _repeat);
			_active = false;
		}
		else
		{
			NextThink = (float)(Server.Globals.Time + _repeat + 0.5);
		}
	}

	public void DelayThink()
	{
		_active = true;

		if ( string.IsNullOrEmpty( TargetName ) )
			NextThink = (float)(Server.Globals.Time + 0.1);

		SetThink( FindThink );
	}

	private bool AcceptableSpeaker( BaseToggle target )
	{
		if ( target is not BaseMonster monster )
			return target is not null && target.IsAllowedToSpeak;

		if ( (SpawnFlags & SpawnFlagFollowers) != 0 && monster.TargetEntity?.ClassName != "player" )
			return false;

		return monster.CanPlaySentence( (SpawnFlags & SpawnFlagInterrupt) != 0 );
	}

	private BaseToggle FindEntity()
	{
		var target = Util.FindEntityByTargetname( null, _entity );

		while ( target is not null )
		{
			if ( target is BaseToggle speaker && AcceptableSpeaker( speaker ) )
				return speaker;

			target = Util.FindEntityByTargetname( target, _entity );
		}

		BaseEntity entity = null;

		while ( (entity = Util.FindEntityInSphere( entity, Origin, _radius )) is not null )
		{
			if ( entity.ClassName != _entity || (entity.EntityFlags & EntityFlags.Monster) != EntityFlags.Monster )
				continue;

			var speaker = entity as BaseToggle;

			if ( AcceptableSpeaker( speaker ) )
				return speaker;
		}

		return null;
	}

	private void StartSentence( BaseToggle target )
	{
		var concurrent = (SpawnFlags & SpawnFlagConcurrent) == 0;
		BaseEntity listener = null;

		if ( !string.IsNullOrEmpty( _listener ) )
			listener = Util.FindEntityGeneric( _listener, target.Origin, _listener == "player" ? 4096f : _radius );

		target.PlayScriptedSentence( _sentence, _duration, _volume, _attenuation, concurrent, listener );

		Util.Alert( AlertType.AiConsole, $"Playing sentence {_sentence} ({_duration:F1})\n" );

		UseTargets( null, UseType.Toggle, 0f );
	}
}

[LinkEntityToClass( "monster_furniture" )]
public class Furniture : BaseMonster
{
	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override int Classify() => ClassNone;

	public void Die()
	{
		SetThink( SubRemove );
		NextThink = (float)Server.Globals.Time;
	}

	public override void Spawn()
	{
		Server.PrecacheModel( ModelName );
		SetModel( ModelName );

		MoveType = MoveType.None;
		Solid = Solid.BBox;
		Health = 80000f;
		DamageMode = DamageAim;
		Effects = 0;
		YawSpeed = 0f;
		Sequence = 0;
		Frame = 0f;

		ResetSequenceInfo();
		Frame = 0f;
		MonsterInit();
	}
}
