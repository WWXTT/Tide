// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

Shader "Hidden/Crest/Framework (Obsolete)"
{
	Properties
	{
		[HideInInspector] _ObsoleteMessage( "Use Crest/Ocean instead.", Float ) = 0
	}
	SubShader
	{
		Pass
		{
			ColorMask 0
		}
	}

	CustomEditor "Crest.ObsoleteShaderGUI"
}
