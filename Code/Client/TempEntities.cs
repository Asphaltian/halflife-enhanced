using System;
using GoldSrc;

namespace HalfLife;

partial class HalfLifeClientDll
{
	private const int TempEntityPoolAddress = 0x1032f3a8;
	private const int TempEntitySize = 0xbfc;

	private int _tempEntFrame;

	public override void StudioEvent( in GoldSrc.Studio.Event studioEvent, Vector3[] attachments )
	{
		switch ( studioEvent.Index )
		{
			case 5001:
				Engine.MuzzleFlash( attachments[0], Common.Atoi( studioEvent.Options ) );
				break;
			case 5011:
				Engine.MuzzleFlash( attachments[1], Common.Atoi( studioEvent.Options ) );
				break;
			case 5021:
				Engine.MuzzleFlash( attachments[2], Common.Atoi( studioEvent.Options ) );
				break;
			case 5031:
				Engine.MuzzleFlash( attachments[3], Common.Atoi( studioEvent.Options ) );
				break;
			case 5002:
				Engine.SparkEffect( attachments[0], Common.Atoi( studioEvent.Options ), -100, 100 );
				break;
			case 5004:
				Engine.PlaySoundByNameAtLocation( studioEvent.Options, 1f, attachments[0] );
				break;
		}
	}

	public override void TempEntUpdate( double frameTime, double clientTime, double clientGravity, ref TempEnt free, ref TempEnt active, Func<TempEnt, bool> addVisibleEntity, Action<TempEnt, float> playSound )
	{
		if ( active is null )
			return;

		Engine.PushPMStates();
		Engine.SetSolidPlayers( -1 );

		_tempEntFrame = (_tempEntFrame + 1) & 31;

		var temp = active;

		if ( frameTime <= 0 )
		{
			for ( ; temp is not null; temp = temp.Next )
			{
				if ( (temp.Flags & TempEntityFlags.NoModel) != TempEntityFlags.NoModel )
					addVisibleEntity( temp );
			}

			Engine.PopPMStates();
			return;
		}

		TempEnt previous = null;

		var fastFrequency = (float)(clientTime * 5.5);
		var gravity = (float)(-frameTime * clientGravity);
		var gravitySlow = gravity * 0.5f;

		while ( temp is not null )
		{
			var alive = true;
			var life = (float)(temp.Die - clientTime);
			var next = temp.Next;

			if ( life < 0 )
			{
				if ( (temp.Flags & TempEntityFlags.FadeOut) == TempEntityFlags.FadeOut )
				{
					if ( temp.RenderMode == RenderMode.Normal )
						temp.RenderMode = RenderMode.TransTexture;

					temp.RenderAmt = (int)(temp.BaselineRenderAmt * (1 + (life * temp.FadeSpeed)));

					if ( temp.RenderAmt <= 0 )
						alive = false;
				}
				else
				{
					alive = false;
				}
			}

			if ( !alive )
			{
				temp.Next = free;
				free = temp;

				if ( previous is null )
					active = next;
				else
					previous.Next = next;

				temp = next;
				continue;
			}

			previous = temp;

			temp.PrevOrigin = temp.Origin;

			if ( (temp.Flags & TempEntityFlags.SparkShower) == TempEntityFlags.SparkShower )
			{
				if ( clientTime > temp.BaselineScale )
				{
					Engine.SparkEffect( temp.Origin, 8, -200, 200 );

					temp.BaselineFrameRate -= 0.1f;

					if ( temp.BaselineFrameRate <= 0.0 )
					{
						temp.Die = (float)clientTime;
					}
					else
					{
						temp.Die = (float)(clientTime + 0.5);
						temp.BaselineScale = (float)(clientTime + 0.1);
					}
				}
			}
			else if ( (temp.Flags & TempEntityFlags.PlyrAttachment) == TempEntityFlags.PlyrAttachment )
			{
				temp.Origin = Engine.GetEntityByIndex( temp.ClientIndex ).Origin + temp.TentOffset;
			}
			else if ( (temp.Flags & TempEntityFlags.SineWave) == TempEntityFlags.SineWave )
			{
				temp.X += (float)(temp.Velocity.x * frameTime);
				temp.Y += (float)(temp.Velocity.y * frameTime);

				temp.Origin.x = (float)(temp.X + (Math.Sin( temp.Velocity.z + (clientTime * temp.PrevFrame) ) * (10 * temp.FrameRate)));
				temp.Origin.y = (float)(temp.Y + (Math.Sin( temp.Velocity.z + fastFrequency + 0.7 ) * (8 * temp.FrameRate)));
				temp.Origin.z += (float)(temp.Velocity.z * frameTime);
			}
			else if ( (temp.Flags & TempEntityFlags.Spiral) == TempEntityFlags.Spiral )
			{
				var address = TempEntityPoolAddress + (temp.Index * TempEntitySize);

				temp.Origin.x += (float)((temp.Velocity.x * frameTime) + (8 * Math.Sin( (clientTime * 20) + address )));
				temp.Origin.y += (float)((temp.Velocity.y * frameTime) + (4 * Math.Sin( (clientTime * 30) + address )));
				temp.Origin.z += (float)(temp.Velocity.z * frameTime);
			}
			else
			{
				for ( var i = 0; i < 3; i++ )
					temp.Origin[i] += (float)(temp.Velocity[i] * frameTime);
			}

			if ( (temp.Flags & TempEntityFlags.SprAnimate) == TempEntityFlags.SprAnimate )
			{
				temp.Frame += (float)(frameTime * temp.FrameRate);

				if ( temp.Frame >= temp.FrameMax )
				{
					temp.Frame -= (int)temp.Frame;

					if ( (temp.Flags & TempEntityFlags.SprAnimateLoop) != TempEntityFlags.SprAnimateLoop )
					{
						temp.Die = (float)clientTime;
						temp = next;
						continue;
					}
				}
			}
			else if ( (temp.Flags & TempEntityFlags.SprCycle) == TempEntityFlags.SprCycle )
			{
				temp.Frame += (float)(frameTime * 10);

				if ( temp.Frame >= temp.FrameMax )
					temp.Frame -= (int)temp.Frame;
			}

			if ( (temp.Flags & TempEntityFlags.Rotate) == TempEntityFlags.Rotate )
			{
				temp.Angles.x += (float)(temp.AngularVelocity.x * frameTime);
				temp.Angles.y += (float)(temp.AngularVelocity.y * frameTime);
				temp.Angles.z += (float)(temp.AngularVelocity.z * frameTime);
			}

			if ( (temp.Flags & (TempEntityFlags.CollideAll | TempEntityFlags.CollideWorld)) != 0 )
			{
				var traceNormal = Vector3.Zero;
				var traceFraction = 1f;

				if ( (temp.Flags & TempEntityFlags.CollideAll) == TempEntityFlags.CollideAll )
				{
					Engine.SetTraceHull( 2 );

					var trace = Engine.PlayerTrace( temp.PrevOrigin, temp.Origin, TraceFlags.StudioBox, -1 );

					if ( trace.Fraction != 1 )
					{
						var physEnt = Engine.GetPhysEnt( trace.Ent );

						if ( trace.Ent == 0 || physEnt.Info != temp.ClientIndex )
						{
							traceFraction = trace.Fraction;
							traceNormal = trace.PlaneNormal;

							temp.HitCallback?.Invoke( temp, trace );
						}
					}
				}
				else if ( (temp.Flags & TempEntityFlags.CollideWorld) == TempEntityFlags.CollideWorld )
				{
					Engine.SetTraceHull( 2 );

					var trace = Engine.PlayerTrace( temp.PrevOrigin, temp.Origin, TraceFlags.StudioBox | TraceFlags.WorldOnly, -1 );

					if ( trace.Fraction != 1 )
					{
						traceFraction = trace.Fraction;
						traceNormal = trace.PlaneNormal;

						if ( (temp.Flags & TempEntityFlags.SparkShower) == TempEntityFlags.SparkShower )
						{
							temp.Velocity *= 0.6f;

							if ( MathLib.Length( temp.Velocity ) < 10 )
								temp.BaselineFrameRate = 0f;
						}

						temp.HitCallback?.Invoke( temp, trace );
					}
				}

				if ( traceFraction != 1 )
				{
					temp.Origin = temp.PrevOrigin + ((float)(traceFraction * frameTime) * temp.Velocity);

					var damp = temp.BounceFactor;

					if ( (temp.Flags & (TempEntityFlags.Gravity | TempEntityFlags.SlowGravity)) != 0 )
					{
						damp *= 0.5f;

						if ( traceNormal.z > 0.9f && temp.Velocity.z <= 0 && temp.Velocity.z >= gravity * 3 )
						{
							damp = 0;
							temp.Flags &= ~(TempEntityFlags.Rotate | TempEntityFlags.Gravity | TempEntityFlags.SlowGravity | TempEntityFlags.CollideWorld | TempEntityFlags.SmokeTrail);
							temp.Angles.x = 0;
							temp.Angles.z = 0;
						}
					}

					if ( temp.HitSound != 0 )
						playSound( temp, damp );

					if ( (temp.Flags & TempEntityFlags.CollideKill) == TempEntityFlags.CollideKill )
					{
						temp.Flags &= ~TempEntityFlags.FadeOut;
						temp.Die = (float)clientTime;
					}
					else
					{
						if ( damp != 0 )
						{
							var projection = Vector3.Dot( temp.Velocity, traceNormal );

							temp.Velocity += -projection * 2 * traceNormal;
							temp.Angles.y = -temp.Angles.y;
						}

						if ( damp != 1 )
						{
							temp.Velocity *= damp;
							temp.Angles *= 0.9f;
						}
					}
				}
			}

			if ( (temp.Flags & TempEntityFlags.Flicker) == TempEntityFlags.Flicker && _tempEntFrame == temp.Effects )
			{
				var light = Engine.AllocDlight( 0 );

				light.Origin = temp.Origin;
				light.Radius = 60f;
				light.R = 255;
				light.G = 120;
				light.B = 0;
				light.Die = (float)(clientTime + 0.01);
			}

			if ( (temp.Flags & TempEntityFlags.SmokeTrail) == TempEntityFlags.SmokeTrail )
				Engine.RocketTrail( temp.PrevOrigin, temp.Origin, 1 );

			if ( (temp.Flags & TempEntityFlags.Gravity) == TempEntityFlags.Gravity )
				temp.Velocity.z += gravity;
			else if ( (temp.Flags & TempEntityFlags.SlowGravity) == TempEntityFlags.SlowGravity )
				temp.Velocity.z += gravitySlow;

			if ( (temp.Flags & TempEntityFlags.ClientCustom) == TempEntityFlags.ClientCustom )
				temp.Callback?.Invoke( temp, (float)frameTime, (float)clientTime );

			if ( (temp.Flags & TempEntityFlags.NoModel) != TempEntityFlags.NoModel && !addVisibleEntity( temp ) && (temp.Flags & TempEntityFlags.Persist) != TempEntityFlags.Persist )
			{
				temp.Die = (float)clientTime;
				temp.Flags &= ~TempEntityFlags.FadeOut;
			}

			temp = next;
		}

		Engine.PopPMStates();
	}
}
