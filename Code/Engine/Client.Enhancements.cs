namespace GoldSrc;

partial class Client
{
	[ConVar( "r_ambientocclusion", ConVarFlags.Saved )]
	public static float AmbientOcclusionEnabled { get; set; }

	[ConVar( "r_modellight", ConVarFlags.Saved )]
	public static float ModelLight { get; set; }

	public static float StudioLight => ModelLight != 0f ? 0f : 1f;

	private AmbientOcclusion _ambientOcclusion;

	private void UpdateEnhancements( CameraComponent camera )
	{
		if ( AmbientOcclusionEnabled != 0f || _ambientOcclusion.IsValid() )
		{
			if ( !_ambientOcclusion.IsValid() )
				_ambientOcclusion = camera.GameObject.AddComponent<AmbientOcclusion>();

			_ambientOcclusion.Enabled = AmbientOcclusionEnabled != 0f;
		}
	}
}
