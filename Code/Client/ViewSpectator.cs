using System;
using GoldSrc;

namespace HalfLife;

partial class HalfLifeClientDll
{
	private const int CamModeRelax = 1;
	private const int CamModeFocus = 2;
	private const float CameraRelaxAngle = 5f;
	private const float CameraFocusAngle = 35f;
	private const int ChaseTraceLoops = 8;

	[SkipHotload]
	private static readonly (string Weapon, string View)[] ViewModelMap =
	[
		("models/p_crossbow.mdl", "models/v_crossbow.mdl"),
		("models/p_crowbar.mdl", "models/v_crowbar.mdl"),
		("models/p_egon.mdl", "models/v_egon.mdl"),
		("models/p_gauss.mdl", "models/v_gauss.mdl"),
		("models/p_9mmhandgun.mdl", "models/v_9mmhandgun.mdl"),
		("models/p_grenade.mdl", "models/v_grenade.mdl"),
		("models/p_hgun.mdl", "models/v_hgun.mdl"),
		("models/p_9mmAR.mdl", "models/v_9mmAR.mdl"),
		("models/p_357.mdl", "models/v_357.mdl"),
		("models/p_rpg.mdl", "models/v_rpg.mdl"),
		("models/p_shotgun.mdl", "models/v_shotgun.mdl"),
		("models/p_squeak.mdl", "models/v_squeak.mdl"),
		("models/p_tripmine.mdl", "models/v_tripmine.mdl"),
		("models/p_satchel_radio.mdl", "models/v_satchel_radio.mdl"),
		("models/p_satchel.mdl", "models/v_satchel.mdl")
	];

	public Vector3 LastViewOrigin;
	public Vector3 LastViewAngles;
	public Vector3 LastClientAngles;
	public Vector3 LastSimOrigin;

	public bool JumpSpectator;
	public Vector3 JumpOrigin;
	public Vector3 JumpAngles;

	private Vector3 _lastChaseAngles;
	private float _viewFrameTime;
	private float _lastChaseDistance;
	private int _cameraMode = CamModeFocus;
	private bool _resetCamera = true;
	private Vector3 _spectatorVelocity;
	private int _lastWeaponModelIndex;
	private int _lastViewModelIndex;

	private Vector3 SmoothInterpolateAngles( Vector3 startAngle, Vector3 endAngle, float degreesPerSec )
	{
		startAngle = MathLib.NormalizeAngles( startAngle );
		endAngle = MathLib.NormalizeAngles( endAngle );

		var finalAngle = default( Vector3 );

		for ( var i = 0; i < 3; i++ )
		{
			var d = endAngle[i] - startAngle[i];

			if ( d > 180f )
				d -= 360f;
			else if ( d < -180f )
				d += 360f;

			var absd = MathF.Abs( d );

			if ( absd > 0.01f )
			{
				var frac = degreesPerSec * _viewFrameTime;
				var threshold = degreesPerSec / 4f;

				if ( absd < threshold )
				{
					var h = absd / threshold;

					h *= h;
					frac *= h;
				}

				if ( frac > absd )
					finalAngle[i] = endAngle[i];
				else if ( d > 0f )
					finalAngle[i] = startAngle[i] + frac;
				else
					finalAngle[i] = startAngle[i] - frac;
			}
			else
			{
				finalAngle[i] = endAngle[i];
			}
		}

		return MathLib.NormalizeAngles( finalAngle );
	}

	private Vector3 GetChaseOrigin( Vector3 angles, Vector3 origin, float distance )
	{
		var maxLoops = ChaseTraceLoops;
		var ignoreEnt = -1;

		MathLib.AngleVectors( angles, out var forward, out _, out _ );

		forward *= -1f;

		var start = origin;
		var end = start + (forward * distance);
		PmTrace trace;

		while ( true )
		{
			trace = Engine.TraceLine( start, end, GoldSrc.PlayerMove.TraceLinePhysEntsOnly, 2, ignoreEnt );

			if ( maxLoops <= 0 )
				break;

			if ( trace.Ent <= 0 )
				break;

			var entity = Engine.GetEntityByIndex( Engine.GetPhysEnt( trace.Ent ).Info );

			if ( entity is null )
				break;

			if ( entity.CurState.Solid == (int)Solid.Bsp && !entity.Player )
				break;

			if ( MathLib.Length( trace.EndPos - end ) < 1f )
				break;

			ignoreEnt = trace.Ent;
			start = trace.EndPos;

			maxLoops--;

			if ( maxLoops <= 0 )
				break;
		}

		_lastChaseDistance = MathLib.Length( trace.EndPos - origin );

		return trace.EndPos + (trace.PlaneNormal * 4f);
	}

	private void GetSingleTargetCam( ClientEntity ent1, ref Vector3 angle, out Vector3 origin )
	{
		var flags = _hud.Spectator.ObserverFlags;
		var deadPlayer = ent1.Player && ent1.CurState.Solid == (int)Solid.Not;
		var dfactor = (flags & Hltv.FlagDramatic) != 0 ? -1f : 1f;
		var distance = 112f + (16f * dfactor);

		if ( (flags & Hltv.FlagFinal) != 0 )
			distance *= 2f;
		else if ( deadPlayer )
			distance *= 1.5f;

		_lastChaseDistance += _viewFrameTime * 32f;

		if ( distance > _lastChaseDistance )
			distance = _lastChaseDistance;

		var newOrigin = ent1.Origin;

		if ( ent1.Player )
			newOrigin.z += deadPlayer ? 2f : 17f;
		else
			newOrigin.z += 8f;

		var newAngle = ent1.Angles;

		if ( (flags & Hltv.FlagFacePlayer) != 0 )
			newAngle.y += 180f;

		newAngle.x += 12.5f * dfactor;

		if ( (flags & Hltv.FlagFinal) != 0 )
			newAngle.x = 22.5f;

		if ( (flags & Hltv.FlagSide) != 0 )
			newAngle.y += 22.5f;
		else
			newAngle.y -= 22.5f;

		angle = SmoothInterpolateAngles( _lastChaseAngles, newAngle, 120f );
		origin = GetChaseOrigin( angle, newOrigin, distance );
	}

	private static float MaxAngleBetweenAngles( Vector3 a1, Vector3 a2 )
	{
		var maxd = 0f;

		a1 = MathLib.NormalizeAngles( a1 );
		a2 = MathLib.NormalizeAngles( a2 );

		for ( var i = 0; i < 3; i++ )
		{
			var d = a2[i] - a1[i];

			if ( d > 180f )
				d -= 360f;
			else if ( d < -180f )
				d += 360f;

			d = MathF.Abs( d );

			if ( d > maxd )
				maxd = d;
		}

		return maxd;
	}

	private void GetDoubleTargetsCam( ClientEntity ent1, ClientEntity ent2, ref Vector3 angle, out Vector3 origin )
	{
		var flags = _hud.Spectator.ObserverFlags;
		var dfactor = (flags & Hltv.FlagDramatic) != 0 ? -1f : 1f;
		var distance = 112f + (16f * dfactor);

		if ( (flags & Hltv.FlagFinal) != 0 )
			distance *= 2f;

		_lastChaseDistance += _viewFrameTime * 32f;

		if ( distance > _lastChaseDistance )
			distance = _lastChaseDistance;

		var newOrigin = ent1.Origin;

		newOrigin.z += ent1.Player ? 17f : 8f;

		var newAngle = MathLib.VectorAngles( ent2.Origin - ent1.Origin );

		newAngle.x = -newAngle.x;
		newAngle.x += 12.5f * dfactor;

		if ( (flags & Hltv.FlagSide) != 0 )
			newAngle.y += 22.5f;
		else
			newAngle.y -= 22.5f;

		_lastChaseAngles = MathLib.NormalizeAngles( _lastChaseAngles );
		newAngle = MathLib.NormalizeAngles( newAngle );

		var d = MaxAngleBetweenAngles( _lastChaseAngles, newAngle );

		if ( d < CameraFocusAngle && _cameraMode == CamModeRelax )
			newAngle = _lastChaseAngles;
		else if ( d < CameraRelaxAngle && _cameraMode == CamModeFocus )
			_cameraMode = CamModeRelax;
		else
			_cameraMode = CamModeFocus;

		if ( _resetCamera || _cameraMode == CamModeRelax )
			angle = newAngle;
		else
			angle = SmoothInterpolateAngles( _lastChaseAngles, newAngle, 180f );

		origin = GetChaseOrigin( newAngle, newOrigin, distance );

		if ( _lastChaseDistance < 64f )
			origin.z += 16f * (1f - (_lastChaseDistance / 64f));
	}

	private void GetDirectedChasePosition( ClientEntity ent1, ClientEntity ent2, bool noSecondTarget, ref Vector3 angle, ref Vector3 origin )
	{
		if ( _resetCamera )
			_lastChaseDistance = 4096f;

		if ( noSecondTarget || (ent1.Player && ent1.CurState.Solid == (int)Solid.Not) )
		{
			GetSingleTargetCam( ent1, ref angle, out origin );
		}
		else if ( ent2 is not null )
		{
			GetDoubleTargetsCam( ent1, ent2, ref angle, out origin );
		}
		else
		{
			var flags = _hud.Spectator.ObserverFlags;
			var dfactor = (flags & Hltv.FlagDramatic) != 0 ? -1f : 1f;
			var distance = 112f + (16f * dfactor);

			if ( (flags & Hltv.FlagFinal) != 0 )
				distance *= 2f;

			_lastChaseDistance += _viewFrameTime * 32f;

			if ( distance > _lastChaseDistance )
				distance = _lastChaseDistance;

			var newOrigin = ent1.Origin;

			newOrigin.z += ent1.Player ? 17f : 8f;

			origin = GetChaseOrigin( angle, newOrigin, distance );
		}

		_lastChaseAngles = angle;
	}

	public void GetChasePos( int target, Vector3? clientAngles, ref Vector3 origin, ref Vector3 angles )
	{
		ClientEntity entity = null;

		if ( target != 0 )
			entity = Engine.GetEntityByIndex( target );

		if ( entity is null )
		{
			angles = JumpAngles;
			origin = JumpOrigin;

			return;
		}

		if ( HudSpectator.AutoDirector != 0f )
		{
			if ( User3 != 0 )
				GetDirectedChasePosition( entity, Engine.GetEntityByIndex( User3 ), false, ref angles, ref origin );
			else
				GetDirectedChasePosition( entity, null, true, ref angles, ref origin );
		}
		else
		{
			if ( clientAngles is null )
			{
				angles = entity.Angles;
				angles.x *= -1f;
			}
			else
			{
				angles = clientAngles.Value;
			}

			origin = entity.Origin;
			origin.z += 28f;

			origin = GetChaseOrigin( angles, origin, ChaseDist );
		}

		_resetCamera = false;
	}

	public void ResetChaseCam() => _resetCamera = true;

	public void GetInEyePos( int target, ref Vector3 origin, ref Vector3 angles )
	{
		if ( target == 0 )
		{
			angles = JumpAngles;
			origin = JumpOrigin;

			return;
		}

		var entity = Engine.GetEntityByIndex( target );

		if ( entity is null )
			return;

		origin = entity.Origin;
		angles = entity.Angles;

		angles.x *= -3f;

		if ( entity.CurState.Solid == (int)Solid.Not )
		{
			angles.z = 80f;
			origin.z += -8f;
		}
		else if ( entity.CurState.UseHull == 1 )
		{
			origin.z += 12f;
		}
		else
		{
			origin.z += 28f;
		}
	}

	private void GetMapFreePosition( Vector3 clientAngles, out Vector3 origin, out Vector3 angles )
	{
		angles = clientAngles;
		angles.x = 51.25f + (38.75f * (angles.x / 90f));

		var spectator = _hud.Spectator;
		var zScaledTarget = new Vector3( spectator.MapOrigin.x, spectator.MapOrigin.y, spectator.MapOrigin.z * ((90f - angles.x) / 90f) );

		MathLib.AngleVectors( angles, out var forward, out _, out _ );
		MathLib.VectorNormalize( ref forward );

		origin = zScaledTarget + (forward * -(4096f / spectator.MapZoom));
	}

	private void GetMapChasePosition( int target, Vector3 clientAngles, ref Vector3 origin, ref Vector3 angles )
	{
		if ( target != 0 )
		{
			var entity = Engine.GetEntityByIndex( target );

			if ( HudSpectator.AutoDirector != 0f )
			{
				GetChasePos( target, clientAngles, ref origin, ref angles );

				origin = entity.Origin;
				angles.x = 45f;
			}
			else
			{
				angles = clientAngles;
				origin = entity.Origin;

				angles.x = 51.25f + (38.75f * (angles.x / 90f));
			}
		}
		else
		{
			angles = clientAngles;
			angles.x = 51.25f + (38.75f * (angles.x / 90f));
		}

		origin.z *= (90f - angles.x) / 90f;
		angles.z = 0f;

		MathLib.AngleVectors( angles, out var forward, out _, out _ );
		MathLib.VectorNormalize( ref forward );

		origin += forward * -1536f;
	}

	private int FindViewModelByWeaponModel( int weaponIndex )
	{
		var weaponModel = Engine.GetModelByIndex( weaponIndex );

		if ( weaponModel is null )
			return 0;

		foreach ( var (weapon, view) in ViewModelMap )
		{
			if ( weapon.StartsWith( weaponModel.Name, StringComparison.OrdinalIgnoreCase ) )
				return Engine.FindModelIndex( view );
		}

		return 0;
	}

	private void CalcSpectatorRefdef( ref RefParams refParams )
	{
		var spectator = _hud.Spectator;
		var entity = Engine.GetEntityByIndex( User2 );

		refParams.OnlyClientDraw = 0;

		LastSimOrigin = refParams.SimOrigin;
		LastClientAngles = refParams.ClientViewAngles;
		LastViewAngles = refParams.ViewAngles;
		LastViewOrigin = refParams.ViewOrigin;

		if ( (User1 == HudSpectator.ObsInEye || HudSpectator.Pip == HudSpectator.InsetInEye) && entity is not null )
		{
			var timeDiff = entity.CurState.MsgTime - entity.PrevState.MsgTime;

			if ( timeDiff > 0f )
			{
				var distance = (entity.PrevState.Origin - entity.CurState.Origin) * (1f / timeDiff);

				_spectatorVelocity = (_spectatorVelocity * 0.9f) + (distance * 0.1f);

				refParams.SimVelocity = _spectatorVelocity;
			}

			if ( Engine.IsSpectateOnly )
			{
				GetInEyePos( User2, ref refParams.SimOrigin, ref refParams.ClientViewAngles );

				refParams.Health = 1;

				var gunModel = Engine.ViewModel;

				if ( _lastWeaponModelIndex != entity.CurState.WeaponModel )
				{
					_lastWeaponModelIndex = entity.CurState.WeaponModel;
					_lastViewModelIndex = FindViewModelByWeaponModel( _lastWeaponModelIndex );

					if ( _lastViewModelIndex != 0 )
					{
						Engine.WeaponAnimation( 0, 0 );
					}
					else
					{
						gunModel.NoModel = true;

						_lastWeaponModelIndex = _lastViewModelIndex = 0;
					}
				}

				if ( _lastViewModelIndex != 0 )
				{
					gunModel.NoModel = false;
					gunModel.ModelIndex = _lastViewModelIndex;
					gunModel.Index = User2;
				}
				else
				{
					gunModel.NoModel = true;
				}
			}
			else
			{
				refParams.ClientViewAngles = entity.Angles;
				refParams.ClientViewAngles.x *= -3f;
			}
		}

		_viewFrameTime = refParams.FrameTime;

		if ( refParams.NextView == 0 )
		{
			switch ( User1 )
			{
				case HudSpectator.ObsChaseLocked:
					GetChasePos( User2, null, ref LastViewOrigin, ref LastViewAngles );
					break;

				case HudSpectator.ObsChaseFree:
					GetChasePos( User2, LastClientAngles, ref LastViewOrigin, ref LastViewAngles );
					break;

				case HudSpectator.ObsRoaming:
					LastViewAngles = LastClientAngles;
					LastViewOrigin = LastSimOrigin;

					spectator.GetDirectorCamera( ref LastViewOrigin, ref LastViewAngles );
					break;

				case HudSpectator.ObsInEye:
					CalcNormalRefdef( ref refParams );
					break;

				case HudSpectator.ObsMapFree:
					refParams.OnlyClientDraw = 1;

					GetMapFreePosition( LastClientAngles, out LastViewOrigin, out LastViewAngles );
					break;

				case HudSpectator.ObsMapChase:
					refParams.OnlyClientDraw = 1;

					GetMapChasePosition( User2, LastClientAngles, ref LastViewOrigin, ref LastViewAngles );
					break;
			}

			if ( HudSpectator.Pip != 0f )
				refParams.NextView = 1;

			spectator.DrawCycle = 0;
		}
		else
		{
			refParams.ViewportX = (int)VguiUtil.XResHd( spectator.OverviewData.InsetWindowX );
			refParams.ViewportY = (int)VguiUtil.YResHd( spectator.OverviewData.InsetWindowY );
			refParams.ViewportWidth = (int)VguiUtil.XResHd( spectator.OverviewData.InsetWindowWidth );
			refParams.ViewportHeight = (int)VguiUtil.YResHd( spectator.OverviewData.InsetWindowHeight );
			refParams.NextView = 0;

			switch ( (int)HudSpectator.Pip )
			{
				case HudSpectator.InsetChaseFree:
					GetChasePos( User2, LastClientAngles, ref LastViewOrigin, ref LastViewAngles );
					break;

				case HudSpectator.InsetInEye:
					CalcNormalRefdef( ref refParams );
					break;

				case HudSpectator.InsetMapFree:
					refParams.OnlyClientDraw = 1;

					GetMapFreePosition( LastClientAngles, out LastViewOrigin, out LastViewAngles );
					break;

				case HudSpectator.InsetMapChase:
					refParams.OnlyClientDraw = 1;

					GetMapChasePosition( User1 == HudSpectator.ObsRoaming ? 0 : User2, LastClientAngles, ref LastViewOrigin, ref LastViewAngles );
					break;
			}

			spectator.DrawCycle = 1;
		}

		refParams.ClientViewAngles = LastClientAngles;
		refParams.ViewAngles = LastViewAngles;
		refParams.ViewOrigin = LastViewOrigin;
	}
}
