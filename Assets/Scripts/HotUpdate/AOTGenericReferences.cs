using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

	// {{ AOT assemblies
	public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
	{
		"MemoryPack.Core.dll",
		"Newtonsoft.Json.dll",
		"System.Core.dll",
		"System.Runtime.CompilerServices.Unsafe.dll",
		"System.dll",
		"UniTask.dll",
		"Unity.InferenceEngine.dll",
		"UnityEngine.CoreModule.dll",
		"UnityEngine.JSONSerializeModule.dll",
		"UnityEngine.UIElementsModule.dll",
		"YooAsset.dll",
		"mscorlib.dll",
	};
	// }}

	// {{ constraint implement type
	// }} 

	// {{ AOT generic types
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.EffectHandlerRegistry.<ResolveAtomTargetsAsync>d__13,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__8,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.EffectHandlerRegistry.<ResolveRewardTargetsAsync>d__11,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__12,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.CostCompensationService.<ExecutePayloadAtomAsync>d__3,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.CostCompensationService.<PayWithCompensationAsync>d__0,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.CurseSystem.<ExecutePayloadAsync>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.CurseSystem.<SettleAsync>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.DelayedEffectScheduler.<Fire>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__21>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ApplyOutcomeRewardsAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ApplySituationGateAsync>d__23>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteAsync>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteFlatAsync>d__14>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__16>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteStepsAsync>d__15>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__24>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<ActivateHeroSkill>d__1,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<ResolveCardCastAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<ResolveSpellEffectsAsync>d__25>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<SettleResponseWindowAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameCore.<EnforceHandLimitAsync>d__59>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameLoopController.<ProcessPriority>d__11>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameLoopController.<ResolveStack>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.HeroSkillSystem.<ActivateAsync>d__14,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__13,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.ProphecySystem.<ResolveAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.StackEngine.<BeginResolution>d__50>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.StackEngine.<FinishResolution>d__52>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.StackEngine.<PlayerPass>d__47>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.StackEngine.<ResolveStack>d__51>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.StackEngine.<ResolveTop>d__58>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.TargetSelectionService.<RequestAsync>d__13,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.TideDelay.<After>d__0>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleAttackArrow.<FadeOutAsync>d__23>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleBoardAssemble.<PlayAsync>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleCellPicker.<ConfirmAsync>d__14>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleController.<RunAiTurnAsync>d__25>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<DeadHoldAsync>d__83,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<HeroFatigueHitAsync>d__87>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<HeroImpactPulseAsync>d__86>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayDeathExitAsync>d__82>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<SinkUnderBoardAsync>d__84,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<WaitVisualsIdleAsync>d__72>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleScreen.<<MakePacedAi>b__125_0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleScreen.<<MakePacedScriptedAi>b__124_0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleScreen.<PickRegionOptionAsync>d__298,System.Nullable<int>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleScreen.<ShowResponsePopupAsync>d__294,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleScreen.<ShowTipPromptAsync>d__110,System.Nullable<byte>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleStageDirector.<TweenLightX>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.CombatChoreographer.<ConfirmTargetAsync>d__39>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.CombatChoreographer.<DelayChainAware>d__62>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.CombatChoreographer.<ReturnAsync>d__59>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.CombatChoreographer.<WaitChainIdleAsync>d__63>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.CombatChoreographer.<WalkAsync>d__66,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.RegionTargetSelector.<Run>d__36,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.RegionTargetSelector.<SelectAsync>d__33,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.RegionTargetSelector.<SelectIndicesAsync>d__34,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.RegionTargetSelector.<SelectOneAsync>d__35,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.ScriptedAi.<ExecuteAsync>d__10,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.ScriptedAi.<TakeTurnAsync>d__9>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<DoCombatAsync>d__37>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<PaceAsync>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<PlayBestAffordableCardAsync>d__23,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<StageCastAsync>d__14>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<StageHoldAsync>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<TakeTurnAsync>d__18>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<TryGraveyardPlayAsync>d__24,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<UseHeroSkillAsync>d__20,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.EffectHandlerRegistry.<ResolveAtomTargetsAsync>d__13,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__8,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.EffectHandlerRegistry.<ResolveRewardTargetsAsync>d__11,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__12,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.CostCompensationService.<ExecutePayloadAtomAsync>d__3,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.CostCompensationService.<PayWithCompensationAsync>d__0,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.CurseSystem.<ExecutePayloadAsync>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.CurseSystem.<SettleAsync>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.DelayedEffectScheduler.<Fire>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__21>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ApplyOutcomeRewardsAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ApplySituationGateAsync>d__23>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteAsync>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteFlatAsync>d__14>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__16>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteStepsAsync>d__15>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__24>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<ActivateHeroSkill>d__1,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<ResolveCardCastAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<ResolveSpellEffectsAsync>d__25>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<SettleResponseWindowAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameCore.<EnforceHandLimitAsync>d__59>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameLoopController.<ProcessPriority>d__11>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameLoopController.<ResolveStack>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.HeroSkillSystem.<ActivateAsync>d__14,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__13,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.ProphecySystem.<ResolveAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.StackEngine.<BeginResolution>d__50>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.StackEngine.<FinishResolution>d__52>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.StackEngine.<PlayerPass>d__47>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.StackEngine.<ResolveStack>d__51>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.StackEngine.<ResolveTop>d__58>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.TargetSelectionService.<RequestAsync>d__13,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.TideDelay.<After>d__0>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleAttackArrow.<FadeOutAsync>d__23>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleBoardAssemble.<PlayAsync>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleCellPicker.<ConfirmAsync>d__14>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleController.<RunAiTurnAsync>d__25>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<DeadHoldAsync>d__83,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<HeroFatigueHitAsync>d__87>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<HeroImpactPulseAsync>d__86>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayDeathExitAsync>d__82>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<SinkUnderBoardAsync>d__84,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<WaitVisualsIdleAsync>d__72>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleScreen.<<MakePacedAi>b__125_0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleScreen.<<MakePacedScriptedAi>b__124_0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleScreen.<PickRegionOptionAsync>d__298,System.Nullable<int>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleScreen.<ShowResponsePopupAsync>d__294,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleScreen.<ShowTipPromptAsync>d__110,System.Nullable<byte>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleStageDirector.<TweenLightX>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.CombatChoreographer.<ConfirmTargetAsync>d__39>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.CombatChoreographer.<DelayChainAware>d__62>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.CombatChoreographer.<ReturnAsync>d__59>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.CombatChoreographer.<WaitChainIdleAsync>d__63>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.CombatChoreographer.<WalkAsync>d__66,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.RegionTargetSelector.<Run>d__36,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.RegionTargetSelector.<SelectAsync>d__33,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.RegionTargetSelector.<SelectIndicesAsync>d__34,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.RegionTargetSelector.<SelectOneAsync>d__35,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.ScriptedAi.<ExecuteAsync>d__10,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.ScriptedAi.<TakeTurnAsync>d__9>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<DoCombatAsync>d__37>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<PaceAsync>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<PlayBestAffordableCardAsync>d__23,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<StageCastAsync>d__14>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<StageHoldAsync>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<TakeTurnAsync>d__18>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<TryGraveyardPlayAsync>d__24,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<UseHeroSkillAsync>d__20,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.Nullable<byte>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.Nullable<int>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<CardCore.Attribute.GuardianRules.<SelectAndBindAsync>d__8>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<CardCore.Attribute.MartyrdomRules.<SelectAndBindAsync>d__7>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleCellPicker.<LoopAsync>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleFieldView.<LingerCastingAsync>d__38>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleFieldView.UnitVisual.<RollStatsAsync>d__27>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleHudRow.<>c__DisplayClass32_0.<<FlashPhase>b__0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<ActivateSkillAsync>d__290>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<DrainNetSelects>d__169>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<FadeDisposeArrowAsync>d__137>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<NotChainCountdownAsync>d__296>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<OnClickUnitEffectButton>d__257>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<OnConcede>d__293>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<OnGraveyardPlay>d__289>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<PickLandColorAsync>d__183>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<PlayTutorialVoiceAsync>d__113>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<PromptModeThenPickCell>d__195>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<PromptModeThenPlay>d__178>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<PromptTutorialCompleteAsync>d__143>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<PromptTutorialRetryAsync>d__141>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<RefreshFieldWhenBoardReady>d__157>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<ShowIntroPanelAfterCombatAsync>d__97>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<StartNetAsync>d__161>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleStageDirector.<MountBoardAndDawnAsync>d__25>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleStageDirector.<SwallowToBattleAsync>d__24>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleZoneProps.<PulseDeckAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.CombatChoreographer.<PerformAsync>d__57>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__38>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.CombatChoreographer.<TrackArrowsAsync>d__64>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.PreMatchScreen.<FlipCoinAsync>d__21>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<CardCore.Attribute.GuardianRules.<SelectAndBindAsync>d__8>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<CardCore.Attribute.MartyrdomRules.<SelectAndBindAsync>d__7>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleCellPicker.<LoopAsync>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleFieldView.<LingerCastingAsync>d__38>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleFieldView.UnitVisual.<RollStatsAsync>d__27>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleHudRow.<>c__DisplayClass32_0.<<FlashPhase>b__0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<ActivateSkillAsync>d__290>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<DrainNetSelects>d__169>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<FadeDisposeArrowAsync>d__137>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<NotChainCountdownAsync>d__296>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<OnClickUnitEffectButton>d__257>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<OnConcede>d__293>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<OnGraveyardPlay>d__289>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<PickLandColorAsync>d__183>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<PlayTutorialVoiceAsync>d__113>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<PromptModeThenPickCell>d__195>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<PromptModeThenPlay>d__178>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<PromptTutorialCompleteAsync>d__143>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<PromptTutorialRetryAsync>d__141>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<RefreshFieldWhenBoardReady>d__157>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<ShowIntroPanelAfterCombatAsync>d__97>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<StartNetAsync>d__161>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleStageDirector.<MountBoardAndDawnAsync>d__25>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleStageDirector.<SwallowToBattleAsync>d__24>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleZoneProps.<PulseDeckAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.CombatChoreographer.<PerformAsync>d__57>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__38>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.CombatChoreographer.<TrackArrowsAsync>d__64>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.PreMatchScreen.<FlipCoinAsync>d__21>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<System.Nullable<byte>>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<System.Nullable<int>>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<byte>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<int>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<object>
	// Cysharp.Threading.Tasks.ITaskPoolNode<object>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.Nullable<byte>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.Nullable<int>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.Nullable<byte>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.Nullable<int>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,int>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.IUniTaskSource<byte>
	// Cysharp.Threading.Tasks.IUniTaskSource<int>
	// Cysharp.Threading.Tasks.IUniTaskSource<object>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.Nullable<byte>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.Nullable<int>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,int>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<byte>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<int>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<object>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.Nullable<byte>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.Nullable<int>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.Nullable<byte>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.Nullable<int>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,int>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<byte>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<int>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<object>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.Nullable<byte>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.Nullable<int>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.Nullable<byte>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.Nullable<int>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,int>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<byte>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<int>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<object>
	// Cysharp.Threading.Tasks.UniTask.WhenAnyPromise.<>c<object,object>
	// Cysharp.Threading.Tasks.UniTask.WhenAnyPromise<object,object>
	// Cysharp.Threading.Tasks.UniTask<System.Nullable<byte>>
	// Cysharp.Threading.Tasks.UniTask<System.Nullable<int>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.Nullable<byte>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.Nullable<int>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,int>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.UniTask<byte>
	// Cysharp.Threading.Tasks.UniTask<int>
	// Cysharp.Threading.Tasks.UniTask<object>
	// Cysharp.Threading.Tasks.UniTaskCompletionSource<object>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<Cysharp.Threading.Tasks.AsyncUnit>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<System.Nullable<byte>>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<System.Nullable<int>>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<byte>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<int>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<object>
	// MemoryPack.Formatters.ArrayFormatter<byte>
	// MemoryPack.Formatters.ArrayFormatter<int>
	// MemoryPack.Formatters.ArrayFormatter<object>
	// MemoryPack.Formatters.ArrayFormatter<uint>
	// MemoryPack.Formatters.NullableFormatter<int>
	// MemoryPack.Formatters.UnmanagedFormatter<int>
	// MemoryPack.IMemoryPackFormatter<object>
	// MemoryPack.IMemoryPackable<object>
	// MemoryPack.MemoryPackFormatter<System.Nullable<int>>
	// MemoryPack.MemoryPackFormatter<int>
	// MemoryPack.MemoryPackFormatter<object>
	// System.Action<CardCore.AI.NeuralEnv.TideAction>
	// System.Action<CardCore.EventManager.SubscriptionEntry>
	// System.Action<Cysharp.Threading.Tasks.UniTask>
	// System.Action<MemoryPack.Internal.BufferSegment>
	// System.Action<MonsterRig.ScatterSim.Piece>
	// System.Action<MonsterRig.WingBreathModule.Wing>
	// System.Action<SynergyUI.BattleCellPicker.CellOption>
	// System.Action<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Action<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Action<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Action<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Action<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Action<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Action<System.ValueTuple<int,float,float>>
	// System.Action<System.ValueTuple<int,int>>
	// System.Action<System.ValueTuple<object,byte>>
	// System.Action<System.ValueTuple<object,int,int,int>>
	// System.Action<System.ValueTuple<object,int>>
	// System.Action<System.ValueTuple<object,object,object>>
	// System.Action<System.ValueTuple<object,object>>
	// System.Action<UnityEngine.Color>
	// System.Action<UnityEngine.CombineInstance>
	// System.Action<UnityEngine.Quaternion>
	// System.Action<UnityEngine.UIElements.RuleMatcher>
	// System.Action<UnityEngine.Vector2>
	// System.Action<UnityEngine.Vector3>
	// System.Action<UnityEngine.Vector4>
	// System.Action<byte>
	// System.Action<float>
	// System.Action<int,int>
	// System.Action<int,object>
	// System.Action<int>
	// System.Action<long>
	// System.Action<object,object,int>
	// System.Action<object,object,object>
	// System.Action<object,object>
	// System.Action<object>
	// System.Action<uint>
	// System.Action<ulong>
	// System.ArraySegment.Enumerator<byte>
	// System.ArraySegment.Enumerator<float>
	// System.ArraySegment.Enumerator<int>
	// System.ArraySegment.Enumerator<object>
	// System.ArraySegment.Enumerator<uint>
	// System.ArraySegment.Enumerator<ushort>
	// System.ArraySegment<byte>
	// System.ArraySegment<float>
	// System.ArraySegment<int>
	// System.ArraySegment<object>
	// System.ArraySegment<uint>
	// System.ArraySegment<ushort>
	// System.Buffers.ArrayPool<byte>
	// System.Buffers.IBufferWriter<byte>
	// System.Buffers.TlsOverPerCoreLockedStacksArrayPool.LockedStack<byte>
	// System.Buffers.TlsOverPerCoreLockedStacksArrayPool.PerCoreLockedStacks<byte>
	// System.Buffers.TlsOverPerCoreLockedStacksArrayPool<byte>
	// System.ByReference<byte>
	// System.ByReference<float>
	// System.ByReference<int>
	// System.ByReference<object>
	// System.ByReference<uint>
	// System.ByReference<ushort>
	// System.Collections.Concurrent.ConcurrentDictionary.<GetEnumerator>d__35<object,object>
	// System.Collections.Concurrent.ConcurrentDictionary.DictionaryEnumerator<object,object>
	// System.Collections.Concurrent.ConcurrentDictionary.Node<object,object>
	// System.Collections.Concurrent.ConcurrentDictionary.Tables<object,object>
	// System.Collections.Concurrent.ConcurrentDictionary<object,object>
	// System.Collections.Concurrent.ConcurrentQueue.<Enumerate>d__28<object>
	// System.Collections.Concurrent.ConcurrentQueue.Segment<object>
	// System.Collections.Concurrent.ConcurrentQueue<object>
	// System.Collections.Generic.ArraySortHelper<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.ArraySortHelper<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.ArraySortHelper<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.ArraySortHelper<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.ArraySortHelper<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.ArraySortHelper<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.ArraySortHelper<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.ArraySortHelper<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<int,int>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,byte>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,int,int,int>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,int>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,object>>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.Color>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.CombineInstance>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.Quaternion>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.Vector2>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.Vector3>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.Vector4>
	// System.Collections.Generic.ArraySortHelper<byte>
	// System.Collections.Generic.ArraySortHelper<float>
	// System.Collections.Generic.ArraySortHelper<int>
	// System.Collections.Generic.ArraySortHelper<long>
	// System.Collections.Generic.ArraySortHelper<object>
	// System.Collections.Generic.ArraySortHelper<uint>
	// System.Collections.Generic.ArraySortHelper<ulong>
	// System.Collections.Generic.Comparer<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.Comparer<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.Comparer<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.Comparer<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.Comparer<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.Comparer<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.Comparer<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.Comparer<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.Comparer<System.Guid>
	// System.Collections.Generic.Comparer<System.Nullable<byte>>
	// System.Collections.Generic.Comparer<System.Nullable<int>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,int>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.Comparer<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.Comparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.Comparer<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,byte>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,int,int,int>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.Comparer<UnityEngine.Color>
	// System.Collections.Generic.Comparer<UnityEngine.CombineInstance>
	// System.Collections.Generic.Comparer<UnityEngine.Quaternion>
	// System.Collections.Generic.Comparer<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.Comparer<UnityEngine.Vector2>
	// System.Collections.Generic.Comparer<UnityEngine.Vector3>
	// System.Collections.Generic.Comparer<UnityEngine.Vector4>
	// System.Collections.Generic.Comparer<byte>
	// System.Collections.Generic.Comparer<float>
	// System.Collections.Generic.Comparer<int>
	// System.Collections.Generic.Comparer<long>
	// System.Collections.Generic.Comparer<object>
	// System.Collections.Generic.Comparer<uint>
	// System.Collections.Generic.Comparer<ulong>
	// System.Collections.Generic.ComparisonComparer<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.ComparisonComparer<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.ComparisonComparer<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.ComparisonComparer<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.ComparisonComparer<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.ComparisonComparer<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.ComparisonComparer<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.ComparisonComparer<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.ComparisonComparer<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.ComparisonComparer<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ComparisonComparer<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ComparisonComparer<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.ComparisonComparer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ComparisonComparer<System.Guid>
	// System.Collections.Generic.ComparisonComparer<System.Nullable<byte>>
	// System.Collections.Generic.ComparisonComparer<System.Nullable<int>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,int>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<object,byte>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<object,int,int,int>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.ComparisonComparer<UnityEngine.Color>
	// System.Collections.Generic.ComparisonComparer<UnityEngine.CombineInstance>
	// System.Collections.Generic.ComparisonComparer<UnityEngine.Quaternion>
	// System.Collections.Generic.ComparisonComparer<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.ComparisonComparer<UnityEngine.Vector2>
	// System.Collections.Generic.ComparisonComparer<UnityEngine.Vector3>
	// System.Collections.Generic.ComparisonComparer<UnityEngine.Vector4>
	// System.Collections.Generic.ComparisonComparer<byte>
	// System.Collections.Generic.ComparisonComparer<float>
	// System.Collections.Generic.ComparisonComparer<int>
	// System.Collections.Generic.ComparisonComparer<long>
	// System.Collections.Generic.ComparisonComparer<object>
	// System.Collections.Generic.ComparisonComparer<uint>
	// System.Collections.Generic.ComparisonComparer<ulong>
	// System.Collections.Generic.Dictionary.Enumerator<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary.Enumerator<System.ValueTuple<int,int>,object>
	// System.Collections.Generic.Dictionary.Enumerator<byte,object>
	// System.Collections.Generic.Dictionary.Enumerator<int,System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.Dictionary.Enumerator<int,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.Enumerator<int,float>
	// System.Collections.Generic.Dictionary.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.Enumerator<long,object>
	// System.Collections.Generic.Dictionary.Enumerator<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.Dictionary.Enumerator<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary.Enumerator<object,byte>
	// System.Collections.Generic.Dictionary.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.Enumerator<uint,System.ValueTuple<int,int>>
	// System.Collections.Generic.Dictionary.Enumerator<uint,System.ValueTuple<int,object>>
	// System.Collections.Generic.Dictionary.Enumerator<uint,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.Enumerator<uint,UnityEngine.Vector2Int>
	// System.Collections.Generic.Dictionary.Enumerator<uint,object>
	// System.Collections.Generic.Dictionary.Enumerator<ulong,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<System.ValueTuple<int,int>,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<byte,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,float>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<long,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,byte>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<uint,System.ValueTuple<int,int>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<uint,System.ValueTuple<int,object>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<uint,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<uint,UnityEngine.Vector2Int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<uint,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<ulong,int>
	// System.Collections.Generic.Dictionary.KeyCollection<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary.KeyCollection<System.ValueTuple<int,int>,object>
	// System.Collections.Generic.Dictionary.KeyCollection<byte,object>
	// System.Collections.Generic.Dictionary.KeyCollection<int,System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.Dictionary.KeyCollection<int,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.KeyCollection<int,float>
	// System.Collections.Generic.Dictionary.KeyCollection<int,int>
	// System.Collections.Generic.Dictionary.KeyCollection<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection<long,object>
	// System.Collections.Generic.Dictionary.KeyCollection<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.Dictionary.KeyCollection<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary.KeyCollection<object,byte>
	// System.Collections.Generic.Dictionary.KeyCollection<object,int>
	// System.Collections.Generic.Dictionary.KeyCollection<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection<uint,System.ValueTuple<int,int>>
	// System.Collections.Generic.Dictionary.KeyCollection<uint,System.ValueTuple<int,object>>
	// System.Collections.Generic.Dictionary.KeyCollection<uint,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.KeyCollection<uint,UnityEngine.Vector2Int>
	// System.Collections.Generic.Dictionary.KeyCollection<uint,object>
	// System.Collections.Generic.Dictionary.KeyCollection<ulong,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<System.ValueTuple<int,int>,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<byte,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,float>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<long,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,byte>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<uint,System.ValueTuple<int,int>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<uint,System.ValueTuple<int,object>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<uint,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<uint,UnityEngine.Vector2Int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<uint,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<ulong,int>
	// System.Collections.Generic.Dictionary.ValueCollection<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary.ValueCollection<System.ValueTuple<int,int>,object>
	// System.Collections.Generic.Dictionary.ValueCollection<byte,object>
	// System.Collections.Generic.Dictionary.ValueCollection<int,System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.Dictionary.ValueCollection<int,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.ValueCollection<int,float>
	// System.Collections.Generic.Dictionary.ValueCollection<int,int>
	// System.Collections.Generic.Dictionary.ValueCollection<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection<long,object>
	// System.Collections.Generic.Dictionary.ValueCollection<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.Dictionary.ValueCollection<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary.ValueCollection<object,byte>
	// System.Collections.Generic.Dictionary.ValueCollection<object,int>
	// System.Collections.Generic.Dictionary.ValueCollection<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection<uint,System.ValueTuple<int,int>>
	// System.Collections.Generic.Dictionary.ValueCollection<uint,System.ValueTuple<int,object>>
	// System.Collections.Generic.Dictionary.ValueCollection<uint,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.ValueCollection<uint,UnityEngine.Vector2Int>
	// System.Collections.Generic.Dictionary.ValueCollection<uint,object>
	// System.Collections.Generic.Dictionary.ValueCollection<ulong,int>
	// System.Collections.Generic.Dictionary<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary<System.ValueTuple<int,int>,object>
	// System.Collections.Generic.Dictionary<byte,object>
	// System.Collections.Generic.Dictionary<int,System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.Dictionary<int,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary<int,float>
	// System.Collections.Generic.Dictionary<int,int>
	// System.Collections.Generic.Dictionary<int,object>
	// System.Collections.Generic.Dictionary<long,object>
	// System.Collections.Generic.Dictionary<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.Dictionary<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary<object,byte>
	// System.Collections.Generic.Dictionary<object,int>
	// System.Collections.Generic.Dictionary<object,object>
	// System.Collections.Generic.Dictionary<uint,System.ValueTuple<int,int>>
	// System.Collections.Generic.Dictionary<uint,System.ValueTuple<int,object>>
	// System.Collections.Generic.Dictionary<uint,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary<uint,UnityEngine.Vector2Int>
	// System.Collections.Generic.Dictionary<uint,object>
	// System.Collections.Generic.Dictionary<ulong,int>
	// System.Collections.Generic.EqualityComparer<System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.EqualityComparer<System.Nullable<byte>>
	// System.Collections.Generic.EqualityComparer<System.Nullable<int>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,int>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<int,byte>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<int,object>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.EqualityComparer<UnityEngine.Color>
	// System.Collections.Generic.EqualityComparer<UnityEngine.Vector2>
	// System.Collections.Generic.EqualityComparer<UnityEngine.Vector2Int>
	// System.Collections.Generic.EqualityComparer<byte>
	// System.Collections.Generic.EqualityComparer<float>
	// System.Collections.Generic.EqualityComparer<int>
	// System.Collections.Generic.EqualityComparer<long>
	// System.Collections.Generic.EqualityComparer<object>
	// System.Collections.Generic.EqualityComparer<uint>
	// System.Collections.Generic.EqualityComparer<ulong>
	// System.Collections.Generic.HashSet.Enumerator<int>
	// System.Collections.Generic.HashSet.Enumerator<long>
	// System.Collections.Generic.HashSet.Enumerator<object>
	// System.Collections.Generic.HashSet.Enumerator<uint>
	// System.Collections.Generic.HashSet<int>
	// System.Collections.Generic.HashSet<long>
	// System.Collections.Generic.HashSet<object>
	// System.Collections.Generic.HashSet<uint>
	// System.Collections.Generic.HashSetEqualityComparer<int>
	// System.Collections.Generic.HashSetEqualityComparer<long>
	// System.Collections.Generic.HashSetEqualityComparer<object>
	// System.Collections.Generic.HashSetEqualityComparer<uint>
	// System.Collections.Generic.ICollection<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.ICollection<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.ICollection<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.ICollection<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.ICollection<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.ICollection<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.ICollection<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.ICollection<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,byte>,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<byte,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,System.ValueTuple<UnityEngine.Color,byte>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,System.ValueTuple<object,object>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<long,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,int>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,int>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,object>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<object,object>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint,UnityEngine.Vector2Int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<ulong,int>>
	// System.Collections.Generic.ICollection<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.ICollection<System.ValueTuple<int,int>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,byte>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,int,int,int>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,int>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,object>>
	// System.Collections.Generic.ICollection<UnityEngine.Color>
	// System.Collections.Generic.ICollection<UnityEngine.CombineInstance>
	// System.Collections.Generic.ICollection<UnityEngine.Quaternion>
	// System.Collections.Generic.ICollection<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.ICollection<UnityEngine.Vector2>
	// System.Collections.Generic.ICollection<UnityEngine.Vector3>
	// System.Collections.Generic.ICollection<UnityEngine.Vector4>
	// System.Collections.Generic.ICollection<byte>
	// System.Collections.Generic.ICollection<double>
	// System.Collections.Generic.ICollection<float>
	// System.Collections.Generic.ICollection<int>
	// System.Collections.Generic.ICollection<long>
	// System.Collections.Generic.ICollection<object>
	// System.Collections.Generic.ICollection<uint>
	// System.Collections.Generic.ICollection<ulong>
	// System.Collections.Generic.IComparer<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.IComparer<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.IComparer<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.IComparer<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.IComparer<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.IComparer<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.IComparer<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.IComparer<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IComparer<System.Guid>
	// System.Collections.Generic.IComparer<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.IComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,byte>>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,int,int,int>>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.IComparer<UnityEngine.Color>
	// System.Collections.Generic.IComparer<UnityEngine.CombineInstance>
	// System.Collections.Generic.IComparer<UnityEngine.Quaternion>
	// System.Collections.Generic.IComparer<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.IComparer<UnityEngine.Vector2>
	// System.Collections.Generic.IComparer<UnityEngine.Vector3>
	// System.Collections.Generic.IComparer<UnityEngine.Vector4>
	// System.Collections.Generic.IComparer<byte>
	// System.Collections.Generic.IComparer<float>
	// System.Collections.Generic.IComparer<int>
	// System.Collections.Generic.IComparer<long>
	// System.Collections.Generic.IComparer<object>
	// System.Collections.Generic.IComparer<uint>
	// System.Collections.Generic.IComparer<ulong>
	// System.Collections.Generic.IDictionary<object,object>
	// System.Collections.Generic.IEnumerable<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.IEnumerable<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.IEnumerable<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.IEnumerable<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.IEnumerable<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.IEnumerable<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.IEnumerable<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.IEnumerable<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,byte>,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<byte,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,System.ValueTuple<UnityEngine.Color,byte>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,System.ValueTuple<object,object>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<long,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,int>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,int>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,object>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<object,object>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<uint,UnityEngine.Vector2Int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<uint,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<ulong,int>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<int,int>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,byte>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,int,int,int>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,int>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>
	// System.Collections.Generic.IEnumerable<UnityEngine.Color>
	// System.Collections.Generic.IEnumerable<UnityEngine.CombineInstance>
	// System.Collections.Generic.IEnumerable<UnityEngine.Quaternion>
	// System.Collections.Generic.IEnumerable<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.IEnumerable<UnityEngine.Vector2>
	// System.Collections.Generic.IEnumerable<UnityEngine.Vector3>
	// System.Collections.Generic.IEnumerable<UnityEngine.Vector4>
	// System.Collections.Generic.IEnumerable<byte>
	// System.Collections.Generic.IEnumerable<double>
	// System.Collections.Generic.IEnumerable<float>
	// System.Collections.Generic.IEnumerable<int>
	// System.Collections.Generic.IEnumerable<long>
	// System.Collections.Generic.IEnumerable<object>
	// System.Collections.Generic.IEnumerable<uint>
	// System.Collections.Generic.IEnumerable<ulong>
	// System.Collections.Generic.IEnumerator<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.IEnumerator<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.IEnumerator<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.IEnumerator<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.IEnumerator<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.IEnumerator<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.IEnumerator<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.IEnumerator<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,byte>,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<byte,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,System.ValueTuple<UnityEngine.Color,byte>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,System.ValueTuple<object,object>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<long,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,int>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,int>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,object>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<object,object>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<uint,UnityEngine.Vector2Int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<uint,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<ulong,int>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<int,int>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,byte>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,int,int,int>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,int>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,object>>
	// System.Collections.Generic.IEnumerator<UnityEngine.Color>
	// System.Collections.Generic.IEnumerator<UnityEngine.CombineInstance>
	// System.Collections.Generic.IEnumerator<UnityEngine.Quaternion>
	// System.Collections.Generic.IEnumerator<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.IEnumerator<UnityEngine.Vector2>
	// System.Collections.Generic.IEnumerator<UnityEngine.Vector3>
	// System.Collections.Generic.IEnumerator<UnityEngine.Vector4>
	// System.Collections.Generic.IEnumerator<byte>
	// System.Collections.Generic.IEnumerator<double>
	// System.Collections.Generic.IEnumerator<float>
	// System.Collections.Generic.IEnumerator<int>
	// System.Collections.Generic.IEnumerator<long>
	// System.Collections.Generic.IEnumerator<object>
	// System.Collections.Generic.IEnumerator<uint>
	// System.Collections.Generic.IEnumerator<ulong>
	// System.Collections.Generic.IEqualityComparer<System.ValueTuple<int,byte>>
	// System.Collections.Generic.IEqualityComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.IEqualityComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.IEqualityComparer<byte>
	// System.Collections.Generic.IEqualityComparer<float>
	// System.Collections.Generic.IEqualityComparer<int>
	// System.Collections.Generic.IEqualityComparer<long>
	// System.Collections.Generic.IEqualityComparer<object>
	// System.Collections.Generic.IEqualityComparer<uint>
	// System.Collections.Generic.IEqualityComparer<ulong>
	// System.Collections.Generic.IList<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.IList<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.IList<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.IList<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.IList<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.IList<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.IList<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.IList<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IList<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.IList<System.ValueTuple<int,int>>
	// System.Collections.Generic.IList<System.ValueTuple<object,byte>>
	// System.Collections.Generic.IList<System.ValueTuple<object,int,int,int>>
	// System.Collections.Generic.IList<System.ValueTuple<object,int>>
	// System.Collections.Generic.IList<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.IList<System.ValueTuple<object,object>>
	// System.Collections.Generic.IList<UnityEngine.Color>
	// System.Collections.Generic.IList<UnityEngine.CombineInstance>
	// System.Collections.Generic.IList<UnityEngine.Quaternion>
	// System.Collections.Generic.IList<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.IList<UnityEngine.Vector2>
	// System.Collections.Generic.IList<UnityEngine.Vector3>
	// System.Collections.Generic.IList<UnityEngine.Vector4>
	// System.Collections.Generic.IList<byte>
	// System.Collections.Generic.IList<double>
	// System.Collections.Generic.IList<float>
	// System.Collections.Generic.IList<int>
	// System.Collections.Generic.IList<long>
	// System.Collections.Generic.IList<object>
	// System.Collections.Generic.IList<uint>
	// System.Collections.Generic.IList<ulong>
	// System.Collections.Generic.IReadOnlyCollection<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.IReadOnlyCollection<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.IReadOnlyCollection<System.Nullable<System.ValueTuple<uint,int,int>>>
	// System.Collections.Generic.IReadOnlyCollection<System.ValueTuple<int,int>>
	// System.Collections.Generic.IReadOnlyCollection<UnityEngine.Vector3>
	// System.Collections.Generic.IReadOnlyCollection<object>
	// System.Collections.Generic.IReadOnlyDictionary<int,float>
	// System.Collections.Generic.IReadOnlyDictionary<object,int>
	// System.Collections.Generic.IReadOnlyList<System.Nullable<System.ValueTuple<uint,int,int>>>
	// System.Collections.Generic.IReadOnlyList<System.ValueTuple<int,int>>
	// System.Collections.Generic.IReadOnlyList<UnityEngine.Vector3>
	// System.Collections.Generic.IReadOnlyList<object>
	// System.Collections.Generic.KeyValuePair<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,object>
	// System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>
	// System.Collections.Generic.KeyValuePair<byte,object>
	// System.Collections.Generic.KeyValuePair<int,System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.KeyValuePair<int,System.ValueTuple<object,object>>
	// System.Collections.Generic.KeyValuePair<int,float>
	// System.Collections.Generic.KeyValuePair<int,int>
	// System.Collections.Generic.KeyValuePair<int,object>
	// System.Collections.Generic.KeyValuePair<long,object>
	// System.Collections.Generic.KeyValuePair<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.KeyValuePair<object,byte>
	// System.Collections.Generic.KeyValuePair<object,int>
	// System.Collections.Generic.KeyValuePair<object,object>
	// System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,int>>
	// System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,object>>
	// System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<object,object>>
	// System.Collections.Generic.KeyValuePair<uint,UnityEngine.Vector2Int>
	// System.Collections.Generic.KeyValuePair<uint,object>
	// System.Collections.Generic.KeyValuePair<ulong,int>
	// System.Collections.Generic.List.Enumerator<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.List.Enumerator<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.List.Enumerator<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.List.Enumerator<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.List.Enumerator<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.List.Enumerator<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.List.Enumerator<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.List.Enumerator<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<int,int>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,byte>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,int,int,int>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,int>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,object>>
	// System.Collections.Generic.List.Enumerator<UnityEngine.Color>
	// System.Collections.Generic.List.Enumerator<UnityEngine.CombineInstance>
	// System.Collections.Generic.List.Enumerator<UnityEngine.Quaternion>
	// System.Collections.Generic.List.Enumerator<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.List.Enumerator<UnityEngine.Vector2>
	// System.Collections.Generic.List.Enumerator<UnityEngine.Vector3>
	// System.Collections.Generic.List.Enumerator<UnityEngine.Vector4>
	// System.Collections.Generic.List.Enumerator<byte>
	// System.Collections.Generic.List.Enumerator<float>
	// System.Collections.Generic.List.Enumerator<int>
	// System.Collections.Generic.List.Enumerator<long>
	// System.Collections.Generic.List.Enumerator<object>
	// System.Collections.Generic.List.Enumerator<uint>
	// System.Collections.Generic.List.Enumerator<ulong>
	// System.Collections.Generic.List<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.List<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.List<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.List<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.List<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.List<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.List<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.List<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.List<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.List<System.ValueTuple<int,int>>
	// System.Collections.Generic.List<System.ValueTuple<object,byte>>
	// System.Collections.Generic.List<System.ValueTuple<object,int,int,int>>
	// System.Collections.Generic.List<System.ValueTuple<object,int>>
	// System.Collections.Generic.List<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.List<System.ValueTuple<object,object>>
	// System.Collections.Generic.List<UnityEngine.Color>
	// System.Collections.Generic.List<UnityEngine.CombineInstance>
	// System.Collections.Generic.List<UnityEngine.Quaternion>
	// System.Collections.Generic.List<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.List<UnityEngine.Vector2>
	// System.Collections.Generic.List<UnityEngine.Vector3>
	// System.Collections.Generic.List<UnityEngine.Vector4>
	// System.Collections.Generic.List<byte>
	// System.Collections.Generic.List<float>
	// System.Collections.Generic.List<int>
	// System.Collections.Generic.List<long>
	// System.Collections.Generic.List<object>
	// System.Collections.Generic.List<uint>
	// System.Collections.Generic.List<ulong>
	// System.Collections.Generic.ObjectComparer<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.ObjectComparer<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.ObjectComparer<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.ObjectComparer<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.ObjectComparer<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.ObjectComparer<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.ObjectComparer<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.ObjectComparer<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ObjectComparer<System.Guid>
	// System.Collections.Generic.ObjectComparer<System.Nullable<byte>>
	// System.Collections.Generic.ObjectComparer<System.Nullable<int>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,int>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,byte>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,int,int,int>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.ObjectComparer<UnityEngine.Color>
	// System.Collections.Generic.ObjectComparer<UnityEngine.CombineInstance>
	// System.Collections.Generic.ObjectComparer<UnityEngine.Quaternion>
	// System.Collections.Generic.ObjectComparer<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.ObjectComparer<UnityEngine.Vector2>
	// System.Collections.Generic.ObjectComparer<UnityEngine.Vector3>
	// System.Collections.Generic.ObjectComparer<UnityEngine.Vector4>
	// System.Collections.Generic.ObjectComparer<byte>
	// System.Collections.Generic.ObjectComparer<float>
	// System.Collections.Generic.ObjectComparer<int>
	// System.Collections.Generic.ObjectComparer<long>
	// System.Collections.Generic.ObjectComparer<object>
	// System.Collections.Generic.ObjectComparer<uint>
	// System.Collections.Generic.ObjectComparer<ulong>
	// System.Collections.Generic.ObjectEqualityComparer<System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.ObjectEqualityComparer<System.Nullable<byte>>
	// System.Collections.Generic.ObjectEqualityComparer<System.Nullable<int>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,int>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<int,byte>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<int,object>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.ObjectEqualityComparer<UnityEngine.Color>
	// System.Collections.Generic.ObjectEqualityComparer<UnityEngine.Vector2>
	// System.Collections.Generic.ObjectEqualityComparer<UnityEngine.Vector2Int>
	// System.Collections.Generic.ObjectEqualityComparer<byte>
	// System.Collections.Generic.ObjectEqualityComparer<float>
	// System.Collections.Generic.ObjectEqualityComparer<int>
	// System.Collections.Generic.ObjectEqualityComparer<long>
	// System.Collections.Generic.ObjectEqualityComparer<object>
	// System.Collections.Generic.ObjectEqualityComparer<uint>
	// System.Collections.Generic.ObjectEqualityComparer<ulong>
	// System.Collections.Generic.Queue.Enumerator<object>
	// System.Collections.Generic.Queue<object>
	// System.Collections.Generic.SortedSet.<>c__DisplayClass52_0<ulong>
	// System.Collections.Generic.SortedSet.<>c__DisplayClass53_0<ulong>
	// System.Collections.Generic.SortedSet.<>c__DisplayClass85_0<ulong>
	// System.Collections.Generic.SortedSet.<Reverse>d__94<ulong>
	// System.Collections.Generic.SortedSet.Enumerator<ulong>
	// System.Collections.Generic.SortedSet.Node<ulong>
	// System.Collections.Generic.SortedSet.TreeSubSet.<>c__DisplayClass9_0<ulong>
	// System.Collections.Generic.SortedSet.TreeSubSet<ulong>
	// System.Collections.Generic.SortedSet<ulong>
	// System.Collections.Generic.SortedSetEqualityComparer<ulong>
	// System.Collections.Generic.Stack.Enumerator<object>
	// System.Collections.Generic.Stack<object>
	// System.Collections.Generic.TreeWalkPredicate<ulong>
	// System.Collections.ObjectModel.ReadOnlyCollection<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.ObjectModel.ReadOnlyCollection<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.ObjectModel.ReadOnlyCollection<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.ObjectModel.ReadOnlyCollection<MemoryPack.Internal.BufferSegment>
	// System.Collections.ObjectModel.ReadOnlyCollection<MonsterRig.ScatterSim.Piece>
	// System.Collections.ObjectModel.ReadOnlyCollection<MonsterRig.WingBreathModule.Wing>
	// System.Collections.ObjectModel.ReadOnlyCollection<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.ObjectModel.ReadOnlyCollection<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<int,float,float>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<int,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,byte>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,int,int,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,object,object>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,object>>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Color>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.CombineInstance>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Quaternion>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Vector2>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Vector3>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Vector4>
	// System.Collections.ObjectModel.ReadOnlyCollection<byte>
	// System.Collections.ObjectModel.ReadOnlyCollection<float>
	// System.Collections.ObjectModel.ReadOnlyCollection<int>
	// System.Collections.ObjectModel.ReadOnlyCollection<long>
	// System.Collections.ObjectModel.ReadOnlyCollection<object>
	// System.Collections.ObjectModel.ReadOnlyCollection<uint>
	// System.Collections.ObjectModel.ReadOnlyCollection<ulong>
	// System.Comparison<CardCore.AI.NeuralEnv.TideAction>
	// System.Comparison<CardCore.EventManager.SubscriptionEntry>
	// System.Comparison<Cysharp.Threading.Tasks.UniTask>
	// System.Comparison<MemoryPack.Internal.BufferSegment>
	// System.Comparison<MonsterRig.ScatterSim.Piece>
	// System.Comparison<MonsterRig.WingBreathModule.Wing>
	// System.Comparison<SynergyUI.BattleCellPicker.CellOption>
	// System.Comparison<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Comparison<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Comparison<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Comparison<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Comparison<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Comparison<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Comparison<System.Guid>
	// System.Comparison<System.Nullable<byte>>
	// System.Comparison<System.Nullable<int>>
	// System.Comparison<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Comparison<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Comparison<System.ValueTuple<byte,byte>>
	// System.Comparison<System.ValueTuple<byte,int>>
	// System.Comparison<System.ValueTuple<byte,object>>
	// System.Comparison<System.ValueTuple<int,float,float>>
	// System.Comparison<System.ValueTuple<int,int>>
	// System.Comparison<System.ValueTuple<int,object,object>>
	// System.Comparison<System.ValueTuple<object,byte>>
	// System.Comparison<System.ValueTuple<object,int,int,int>>
	// System.Comparison<System.ValueTuple<object,int>>
	// System.Comparison<System.ValueTuple<object,object,object>>
	// System.Comparison<System.ValueTuple<object,object>>
	// System.Comparison<UnityEngine.Color>
	// System.Comparison<UnityEngine.CombineInstance>
	// System.Comparison<UnityEngine.Quaternion>
	// System.Comparison<UnityEngine.UIElements.RuleMatcher>
	// System.Comparison<UnityEngine.Vector2>
	// System.Comparison<UnityEngine.Vector3>
	// System.Comparison<UnityEngine.Vector4>
	// System.Comparison<byte>
	// System.Comparison<float>
	// System.Comparison<int>
	// System.Comparison<long>
	// System.Comparison<object>
	// System.Comparison<uint>
	// System.Comparison<ulong>
	// System.Func<Cysharp.Threading.Tasks.UniTask>
	// System.Func<Cysharp.Threading.Tasks.UniTaskVoid>
	// System.Func<System.Collections.Generic.KeyValuePair<int,float>,byte>
	// System.Func<System.Collections.Generic.KeyValuePair<int,float>,float>
	// System.Func<System.Collections.Generic.KeyValuePair<int,float>,object>
	// System.Func<System.Collections.Generic.KeyValuePair<int,int>,byte>
	// System.Func<System.Collections.Generic.KeyValuePair<int,int>,float>
	// System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>
	// System.Func<System.Collections.Generic.KeyValuePair<int,int>,object>
	// System.Func<System.Collections.Generic.KeyValuePair<int,object>,System.Collections.Generic.KeyValuePair<int,object>>
	// System.Func<System.Collections.Generic.KeyValuePair<int,object>,byte>
	// System.Func<System.Collections.Generic.KeyValuePair<int,object>,int>
	// System.Func<System.Collections.Generic.KeyValuePair<int,object>,object>
	// System.Func<System.Collections.Generic.KeyValuePair<object,int>,byte>
	// System.Func<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Func<System.Nullable<UnityEngine.Vector3>>
	// System.Func<System.Nullable<byte>>
	// System.Func<System.Nullable<int>>
	// System.Func<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Func<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Func<System.ValueTuple<byte,byte>>
	// System.Func<System.ValueTuple<byte,int>>
	// System.Func<System.ValueTuple<byte,object>>
	// System.Func<System.ValueTuple<int,float,float>,float>
	// System.Func<System.ValueTuple<int,float,float>,int>
	// System.Func<System.ValueTuple<int,int>,byte>
	// System.Func<System.ValueTuple<int,int>,int>
	// System.Func<System.ValueTuple<int,int>,object>
	// System.Func<System.ValueTuple<int,object,object>>
	// System.Func<System.ValueTuple<object,int>,byte>
	// System.Func<System.ValueTuple<object,int>,object>
	// System.Func<System.ValueTuple<object,object,object>,byte>
	// System.Func<System.ValueTuple<object,object>,System.ValueTuple<object,object>>
	// System.Func<System.ValueTuple<object,object>,byte>
	// System.Func<System.ValueTuple<object,object>,object>
	// System.Func<UnityEngine.Vector4,byte>
	// System.Func<byte,byte>
	// System.Func<byte>
	// System.Func<double,byte>
	// System.Func<float,byte>
	// System.Func<float,double>
	// System.Func<float,float>
	// System.Func<int,UnityEngine.Vector4>
	// System.Func<int,byte>
	// System.Func<int,float>
	// System.Func<int,int,object>
	// System.Func<int,int>
	// System.Func<int,object>
	// System.Func<int>
	// System.Func<object,Cysharp.Threading.Tasks.UniTask>
	// System.Func<object,System.DateTime>
	// System.Func<object,System.Guid>
	// System.Func<object,System.Nullable<System.ValueTuple<int,int>>>
	// System.Func<object,System.Nullable<byte>>
	// System.Func<object,System.Nullable<int>>
	// System.Func<object,System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Func<object,System.ValueTuple<byte,System.Nullable<int>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Func<object,System.ValueTuple<byte,byte>>
	// System.Func<object,System.ValueTuple<byte,int>>
	// System.Func<object,System.ValueTuple<byte,object>>
	// System.Func<object,System.ValueTuple<int,object,object>>
	// System.Func<object,System.ValueTuple<object,object>>
	// System.Func<object,byte>
	// System.Func<object,float>
	// System.Func<object,int,byte>
	// System.Func<object,int>
	// System.Func<object,object,Cysharp.Threading.Tasks.UniTask<object>>
	// System.Func<object,object,int,object>
	// System.Func<object,object,object,object>
	// System.Func<object,object,object>
	// System.Func<object,object>
	// System.Func<object,uint>
	// System.Func<object>
	// System.Func<uint,byte>
	// System.IComparable<CardCore.TimestampInfo>
	// System.IEquatable<CardCore.ColorIdentity>
	// System.IEquatable<CardCore.TimestampInfo>
	// System.Linq.Buffer<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.Buffer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.Buffer<System.ValueTuple<int,float,float>>
	// System.Linq.Buffer<System.ValueTuple<int,int>>
	// System.Linq.Buffer<System.ValueTuple<object,object>>
	// System.Linq.Buffer<UnityEngine.Vector4>
	// System.Linq.Buffer<int>
	// System.Linq.Buffer<object>
	// System.Linq.Buffer<uint>
	// System.Linq.Enumerable.<CastIterator>d__99<object>
	// System.Linq.Enumerable.<ConcatIterator>d__59<object>
	// System.Linq.Enumerable.<DistinctIterator>d__68<int>
	// System.Linq.Enumerable.<DistinctIterator>d__68<object>
	// System.Linq.Enumerable.<ExceptIterator>d__77<int>
	// System.Linq.Enumerable.<OfTypeIterator>d__97<object>
	// System.Linq.Enumerable.<ReverseIterator>d__79<object>
	// System.Linq.Enumerable.<SelectManyIterator>d__17<object,object>
	// System.Linq.Enumerable.<SkipIterator>d__31<int>
	// System.Linq.Enumerable.<SkipIterator>d__31<object>
	// System.Linq.Enumerable.<TakeIterator>d__25<int>
	// System.Linq.Enumerable.<TakeIterator>d__25<object>
	// System.Linq.Enumerable.<TakeLastIterator>d__218<object>
	// System.Linq.Enumerable.<WhereIterator>d__2<object>
	// System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.Enumerable.Iterator<System.ValueTuple<int,int>>
	// System.Linq.Enumerable.Iterator<System.ValueTuple<object,int>>
	// System.Linq.Enumerable.Iterator<System.ValueTuple<object,object>>
	// System.Linq.Enumerable.Iterator<UnityEngine.Vector4>
	// System.Linq.Enumerable.Iterator<byte>
	// System.Linq.Enumerable.Iterator<double>
	// System.Linq.Enumerable.Iterator<float>
	// System.Linq.Enumerable.Iterator<int>
	// System.Linq.Enumerable.Iterator<object>
	// System.Linq.Enumerable.Iterator<uint>
	// System.Linq.Enumerable.WhereArrayIterator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.Enumerable.WhereArrayIterator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Linq.Enumerable.WhereArrayIterator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.Enumerable.WhereArrayIterator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.Enumerable.WhereArrayIterator<System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereArrayIterator<float>
	// System.Linq.Enumerable.WhereArrayIterator<int>
	// System.Linq.Enumerable.WhereArrayIterator<object>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereEnumerableIterator<UnityEngine.Vector4>
	// System.Linq.Enumerable.WhereEnumerableIterator<byte>
	// System.Linq.Enumerable.WhereEnumerableIterator<double>
	// System.Linq.Enumerable.WhereEnumerableIterator<float>
	// System.Linq.Enumerable.WhereEnumerableIterator<int>
	// System.Linq.Enumerable.WhereEnumerableIterator<object>
	// System.Linq.Enumerable.WhereEnumerableIterator<uint>
	// System.Linq.Enumerable.WhereListIterator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.Enumerable.WhereListIterator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Linq.Enumerable.WhereListIterator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.Enumerable.WhereListIterator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.Enumerable.WhereListIterator<System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereListIterator<float>
	// System.Linq.Enumerable.WhereListIterator<int>
	// System.Linq.Enumerable.WhereListIterator<object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<int,float>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<int,int>,int>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<int,int>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.ValueTuple<int,int>,int>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.ValueTuple<int,int>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.ValueTuple<object,int>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.ValueTuple<object,object>,System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.ValueTuple<object,object>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<float,double>
	// System.Linq.Enumerable.WhereSelectArrayIterator<float,float>
	// System.Linq.Enumerable.WhereSelectArrayIterator<int,UnityEngine.Vector4>
	// System.Linq.Enumerable.WhereSelectArrayIterator<int,int>
	// System.Linq.Enumerable.WhereSelectArrayIterator<int,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,byte>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,float>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,uint>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<int,float>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<int,int>,int>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<int,int>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.ValueTuple<int,int>,int>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.ValueTuple<int,int>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.ValueTuple<object,int>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.ValueTuple<object,object>,System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.ValueTuple<object,object>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<float,double>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<float,float>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<int,UnityEngine.Vector4>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<int,int>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<int,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,byte>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,float>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,uint>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<int,float>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<int,int>,int>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<int,int>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.ValueTuple<int,int>,int>
	// System.Linq.Enumerable.WhereSelectListIterator<System.ValueTuple<int,int>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.ValueTuple<object,int>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.ValueTuple<object,object>,System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereSelectListIterator<System.ValueTuple<object,object>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<float,double>
	// System.Linq.Enumerable.WhereSelectListIterator<float,float>
	// System.Linq.Enumerable.WhereSelectListIterator<int,UnityEngine.Vector4>
	// System.Linq.Enumerable.WhereSelectListIterator<int,int>
	// System.Linq.Enumerable.WhereSelectListIterator<int,object>
	// System.Linq.Enumerable.WhereSelectListIterator<object,System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereSelectListIterator<object,byte>
	// System.Linq.Enumerable.WhereSelectListIterator<object,float>
	// System.Linq.Enumerable.WhereSelectListIterator<object,object>
	// System.Linq.Enumerable.WhereSelectListIterator<object,uint>
	// System.Linq.EnumerableSorter<System.Collections.Generic.KeyValuePair<int,float>,float>
	// System.Linq.EnumerableSorter<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.EnumerableSorter<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.EnumerableSorter<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.EnumerableSorter<System.ValueTuple<int,float,float>,float>
	// System.Linq.EnumerableSorter<System.ValueTuple<int,float,float>>
	// System.Linq.EnumerableSorter<System.ValueTuple<int,int>,int>
	// System.Linq.EnumerableSorter<System.ValueTuple<int,int>>
	// System.Linq.EnumerableSorter<int,float>
	// System.Linq.EnumerableSorter<int,int>
	// System.Linq.EnumerableSorter<int>
	// System.Linq.EnumerableSorter<object,System.Guid>
	// System.Linq.EnumerableSorter<object,byte>
	// System.Linq.EnumerableSorter<object,float>
	// System.Linq.EnumerableSorter<object,int>
	// System.Linq.EnumerableSorter<object,object>
	// System.Linq.EnumerableSorter<object,uint>
	// System.Linq.EnumerableSorter<object>
	// System.Linq.GroupedEnumerable<System.Collections.Generic.KeyValuePair<int,object>,int,System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.GroupedEnumerable<System.ValueTuple<object,object>,System.ValueTuple<object,object>,System.ValueTuple<object,object>>
	// System.Linq.GroupedEnumerable<object,int,object>
	// System.Linq.GroupedEnumerable<object,object,object>
	// System.Linq.IGrouping<System.ValueTuple<object,object>,System.ValueTuple<object,object>>
	// System.Linq.IGrouping<int,System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.IGrouping<int,object>
	// System.Linq.IGrouping<object,object>
	// System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>>
	// System.Linq.IOrderedEnumerable<System.ValueTuple<int,int>>
	// System.Linq.IOrderedEnumerable<object>
	// System.Linq.IdentityFunction.<>c<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.IdentityFunction.<>c<System.ValueTuple<object,object>>
	// System.Linq.IdentityFunction.<>c<object>
	// System.Linq.IdentityFunction<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.IdentityFunction<System.ValueTuple<object,object>>
	// System.Linq.IdentityFunction<object>
	// System.Linq.Lookup.<GetEnumerator>d__12<System.ValueTuple<object,object>,System.ValueTuple<object,object>>
	// System.Linq.Lookup.<GetEnumerator>d__12<int,System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.Lookup.<GetEnumerator>d__12<int,object>
	// System.Linq.Lookup.<GetEnumerator>d__12<object,object>
	// System.Linq.Lookup.Grouping.<GetEnumerator>d__7<System.ValueTuple<object,object>,System.ValueTuple<object,object>>
	// System.Linq.Lookup.Grouping.<GetEnumerator>d__7<int,System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.Lookup.Grouping.<GetEnumerator>d__7<int,object>
	// System.Linq.Lookup.Grouping.<GetEnumerator>d__7<object,object>
	// System.Linq.Lookup.Grouping<System.ValueTuple<object,object>,System.ValueTuple<object,object>>
	// System.Linq.Lookup.Grouping<int,System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.Lookup.Grouping<int,object>
	// System.Linq.Lookup.Grouping<object,object>
	// System.Linq.Lookup<System.ValueTuple<object,object>,System.ValueTuple<object,object>>
	// System.Linq.Lookup<int,System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.Lookup<int,object>
	// System.Linq.Lookup<object,object>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<System.ValueTuple<int,float,float>>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<System.ValueTuple<int,int>>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<int>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<object>
	// System.Linq.OrderedEnumerable<System.Collections.Generic.KeyValuePair<int,float>,float>
	// System.Linq.OrderedEnumerable<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.OrderedEnumerable<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.OrderedEnumerable<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.OrderedEnumerable<System.ValueTuple<int,float,float>,float>
	// System.Linq.OrderedEnumerable<System.ValueTuple<int,float,float>>
	// System.Linq.OrderedEnumerable<System.ValueTuple<int,int>,int>
	// System.Linq.OrderedEnumerable<System.ValueTuple<int,int>>
	// System.Linq.OrderedEnumerable<int,float>
	// System.Linq.OrderedEnumerable<int,int>
	// System.Linq.OrderedEnumerable<int>
	// System.Linq.OrderedEnumerable<object,System.Guid>
	// System.Linq.OrderedEnumerable<object,byte>
	// System.Linq.OrderedEnumerable<object,float>
	// System.Linq.OrderedEnumerable<object,int>
	// System.Linq.OrderedEnumerable<object,object>
	// System.Linq.OrderedEnumerable<object,uint>
	// System.Linq.OrderedEnumerable<object>
	// System.Linq.Set<int>
	// System.Linq.Set<object>
	// System.Nullable<CardCore.Attribute.MorphSnapshot>
	// System.Nullable<CardCore.ComposerCatalog.EngineHeaderPreset>
	// System.Nullable<System.DateTime>
	// System.Nullable<System.ValueTuple<int,int>>
	// System.Nullable<System.ValueTuple<uint,int,int>>
	// System.Nullable<UnityEngine.Color>
	// System.Nullable<UnityEngine.Pose>
	// System.Nullable<UnityEngine.Vector2>
	// System.Nullable<UnityEngine.Vector3>
	// System.Nullable<byte>
	// System.Nullable<float>
	// System.Nullable<int>
	// System.Nullable<ulong>
	// System.Predicate<CardCore.AI.NeuralEnv.TideAction>
	// System.Predicate<CardCore.EventManager.SubscriptionEntry>
	// System.Predicate<Cysharp.Threading.Tasks.UniTask>
	// System.Predicate<MemoryPack.Internal.BufferSegment>
	// System.Predicate<MonsterRig.ScatterSim.Piece>
	// System.Predicate<MonsterRig.WingBreathModule.Wing>
	// System.Predicate<SynergyUI.BattleCellPicker.CellOption>
	// System.Predicate<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Predicate<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Predicate<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Predicate<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Predicate<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Predicate<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Predicate<System.ValueTuple<int,float,float>>
	// System.Predicate<System.ValueTuple<int,int>>
	// System.Predicate<System.ValueTuple<object,byte>>
	// System.Predicate<System.ValueTuple<object,int,int,int>>
	// System.Predicate<System.ValueTuple<object,int>>
	// System.Predicate<System.ValueTuple<object,object,object>>
	// System.Predicate<System.ValueTuple<object,object>>
	// System.Predicate<UnityEngine.Color>
	// System.Predicate<UnityEngine.CombineInstance>
	// System.Predicate<UnityEngine.Quaternion>
	// System.Predicate<UnityEngine.UIElements.RuleMatcher>
	// System.Predicate<UnityEngine.Vector2>
	// System.Predicate<UnityEngine.Vector3>
	// System.Predicate<UnityEngine.Vector4>
	// System.Predicate<byte>
	// System.Predicate<float>
	// System.Predicate<int>
	// System.Predicate<long>
	// System.Predicate<object>
	// System.Predicate<uint>
	// System.Predicate<ulong>
	// System.ReadOnlySpan.Enumerator<byte>
	// System.ReadOnlySpan.Enumerator<float>
	// System.ReadOnlySpan.Enumerator<int>
	// System.ReadOnlySpan.Enumerator<object>
	// System.ReadOnlySpan.Enumerator<uint>
	// System.ReadOnlySpan.Enumerator<ushort>
	// System.ReadOnlySpan<byte>
	// System.ReadOnlySpan<float>
	// System.ReadOnlySpan<int>
	// System.ReadOnlySpan<object>
	// System.ReadOnlySpan<uint>
	// System.ReadOnlySpan<ushort>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Nullable<byte>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Nullable<int>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<byte>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<int>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<object>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.Nullable<byte>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.Nullable<int>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<byte>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<int>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<object>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.Nullable<byte>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.Nullable<int>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<byte>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<int>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<object>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.Nullable<byte>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.Nullable<int>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<byte>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<int>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<object>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.Nullable<byte>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.Nullable<int>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<byte>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<int>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<object>
	// System.Runtime.CompilerServices.TaskAwaiter<System.Nullable<byte>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.Nullable<int>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.TaskAwaiter<byte>
	// System.Runtime.CompilerServices.TaskAwaiter<int>
	// System.Runtime.CompilerServices.TaskAwaiter<object>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.Nullable<byte>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.Nullable<int>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<byte>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<int>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<object>
	// System.Span<byte>
	// System.Span<float>
	// System.Span<int>
	// System.Span<object>
	// System.Span<uint>
	// System.Span<ushort>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.Nullable<byte>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.Nullable<int>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<byte>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<int>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<object>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.Nullable<byte>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.Nullable<int>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.Sources.IValueTaskSource<byte>
	// System.Threading.Tasks.Sources.IValueTaskSource<int>
	// System.Threading.Tasks.Sources.IValueTaskSource<object>
	// System.Threading.Tasks.Task<System.Nullable<byte>>
	// System.Threading.Tasks.Task<System.Nullable<int>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.Task<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.Task<byte>
	// System.Threading.Tasks.Task<int>
	// System.Threading.Tasks.Task<object>
	// System.Threading.Tasks.TaskCompletionSource<System.Nullable<byte>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.Nullable<byte>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.Nullable<int>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<byte>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<int>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<object>
	// System.Threading.Tasks.TaskFactory<System.Nullable<byte>>
	// System.Threading.Tasks.TaskFactory<System.Nullable<int>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.TaskFactory<byte>
	// System.Threading.Tasks.TaskFactory<int>
	// System.Threading.Tasks.TaskFactory<object>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.Nullable<byte>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.Nullable<int>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<byte>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<int>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<object>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.Nullable<byte>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.Nullable<int>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<byte>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<int>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<object>
	// System.Threading.Tasks.ValueTask<System.Nullable<byte>>
	// System.Threading.Tasks.ValueTask<System.Nullable<int>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.Nullable<byte>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.Nullable<int>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.ValueTask<byte>
	// System.Threading.Tasks.ValueTask<int>
	// System.Threading.Tasks.ValueTask<object>
	// System.Threading.ThreadLocal.FinalizationHelper<object>
	// System.Threading.ThreadLocal.IdManager<object>
	// System.Threading.ThreadLocal.LinkedSlot<object>
	// System.Threading.ThreadLocal<object>
	// System.ValueTuple<UnityEngine.Color,byte>
	// System.ValueTuple<UnityEngine.Color,int>
	// System.ValueTuple<UnityEngine.Vector2,UnityEngine.Vector2,UnityEngine.Vector2,UnityEngine.Vector2,UnityEngine.Vector2>
	// System.ValueTuple<byte,System.Nullable<byte>>
	// System.ValueTuple<byte,System.Nullable<int>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<byte>>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.Nullable<int>>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,byte>>
	// System.ValueTuple<byte,System.ValueTuple<byte,int>>
	// System.ValueTuple<byte,System.ValueTuple<byte,object>>
	// System.ValueTuple<byte,System.ValueTuple<int,object,object>>
	// System.ValueTuple<byte,byte>
	// System.ValueTuple<byte,int>
	// System.ValueTuple<byte,object>
	// System.ValueTuple<int,byte>
	// System.ValueTuple<int,float,float>
	// System.ValueTuple<int,int,int>
	// System.ValueTuple<int,int>
	// System.ValueTuple<int,object,object>
	// System.ValueTuple<int,object>
	// System.ValueTuple<object,byte>
	// System.ValueTuple<object,int,int,int>
	// System.ValueTuple<object,int>
	// System.ValueTuple<object,object,int>
	// System.ValueTuple<object,object,object>
	// System.ValueTuple<object,object>
	// System.ValueTuple<uint,int,int>
	// Unity.Collections.NativeArray.Enumerator<float>
	// Unity.Collections.NativeArray.ReadOnly.Enumerator<float>
	// Unity.Collections.NativeArray.ReadOnly<float>
	// Unity.Collections.NativeArray<float>
	// Unity.InferenceEngine.Tensor<float>
	// UnityEngine.Awaitable.<>c<object>
	// UnityEngine.Awaitable.AwaitableAsyncMethodBuilder.IStateMachineBox<object>
	// UnityEngine.Awaitable.AwaitableAsyncMethodBuilder<object>
	// UnityEngine.Awaitable.Awaiter<object>
	// UnityEngine.Awaitable<object>
	// UnityEngine.Events.InvokableCall<byte>
	// UnityEngine.Events.InvokableCall<float>
	// UnityEngine.Events.InvokableCall<int>
	// UnityEngine.Events.InvokableCall<object>
	// UnityEngine.Events.UnityAction<byte>
	// UnityEngine.Events.UnityAction<float>
	// UnityEngine.Events.UnityAction<int>
	// UnityEngine.Events.UnityAction<object>
	// UnityEngine.Events.UnityEvent<byte>
	// UnityEngine.Events.UnityEvent<float>
	// UnityEngine.Events.UnityEvent<int>
	// UnityEngine.Events.UnityEvent<object>
	// UnityEngine.UIElements.ChangeEvent.<>c<object>
	// UnityEngine.UIElements.ChangeEvent<object>
	// UnityEngine.UIElements.EventBase.<>c<object>
	// UnityEngine.UIElements.EventBase<object>
	// UnityEngine.UIElements.EventCallback<object>
	// UnityEngine.UIElements.EventCallbackFunctor<object>
	// UnityEngine.UIElements.INotifyValueChanged<object>
	// UnityEngine.UIElements.ObjectPool.<>c<object>
	// UnityEngine.UIElements.ObjectPool<object>
	// UnityEngine.UIElements.UQueryState.ActionQueryMatcher<object>
	// UnityEngine.UIElements.UQueryState.Enumerator<object>
	// UnityEngine.UIElements.UQueryState.ListQueryMatcher<object,object>
	// UnityEngine.UIElements.UQueryState<object>
	// }}

	public void RefMethods()
	{
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.CurseSystem.<ExecutePayloadAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.CurseSystem.<ExecutePayloadAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.CurseSystem.<SettleAsync>d__12>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.CurseSystem.<SettleAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__21>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ApplyOutcomeRewardsAsync>d__22>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ApplyOutcomeRewardsAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ApplySituationGateAsync>d__23>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ApplySituationGateAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ExecuteAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ExecuteAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ExecuteFlatAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ExecuteFlatAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__16>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ExecuteStepsAsync>d__15>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ExecuteStepsAsync>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameActions.<ResolveCardCastAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameActions.<ResolveCardCastAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameActions.<ResolveSpellEffectsAsync>d__25>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameActions.<ResolveSpellEffectsAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameActions.<SettleResponseWindowAsync>d__22>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameActions.<SettleResponseWindowAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameLoopController.<ProcessPriority>d__11>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameLoopController.<ProcessPriority>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameLoopController.<ResolveStack>d__12>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameLoopController.<ResolveStack>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.StackEngine.<BeginResolution>d__50>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.StackEngine.<BeginResolution>d__50&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.StackEngine.<FinishResolution>d__52>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.StackEngine.<FinishResolution>d__52&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.StackEngine.<PlayerPass>d__47>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.StackEngine.<PlayerPass>d__47&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.StackEngine.<ResolveStack>d__51>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.StackEngine.<ResolveStack>d__51&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.StackEngine.<ResolveTop>d__58>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.StackEngine.<ResolveTop>d__58&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleCellPicker.<ConfirmAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleCellPicker.<ConfirmAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleController.<RunAiTurnAsync>d__25>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleController.<RunAiTurnAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleScreen.<<MakePacedAi>b__125_0>d>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleScreen.<<MakePacedAi>b__125_0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleScreen.<<MakePacedScriptedAi>b__124_0>d>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleScreen.<<MakePacedScriptedAi>b__124_0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.CombatChoreographer.<ConfirmTargetAsync>d__39>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.CombatChoreographer.<ConfirmTargetAsync>d__39&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.ScriptedAi.<TakeTurnAsync>d__9>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.ScriptedAi.<TakeTurnAsync>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<DoCombatAsync>d__37>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<DoCombatAsync>d__37&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<PaceAsync>d__12>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<PaceAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<StageCastAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<StageCastAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<StageHoldAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<StageHoldAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<TakeTurnAsync>d__18>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<TakeTurnAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.CurseSystem.<ExecutePayloadAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.CurseSystem.<ExecutePayloadAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.DelayedEffectScheduler.<Fire>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.DelayedEffectScheduler.<Fire>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.EffectExecutor.<ExecuteAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.EffectExecutor.<ExecuteAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.EffectExecutor.<ExecuteFlatAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.EffectExecutor.<ExecuteFlatAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__16>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__24>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.GameActions.<ResolveCardCastAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.GameActions.<ResolveCardCastAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.ProphecySystem.<ResolveAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.ProphecySystem.<ResolveAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.BattleFieldView.<PlayDeathExitAsync>d__82>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.BattleFieldView.<PlayDeathExitAsync>d__82&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.CombatChoreographer.<ReturnAsync>d__59>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.CombatChoreographer.<ReturnAsync>d__59&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.ScriptedAi.<TakeTurnAsync>d__9>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.ScriptedAi.<TakeTurnAsync>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.SimpleAI.<TakeTurnAsync>d__18>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.SimpleAI.<TakeTurnAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.EffectExecutor.<ExecuteAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.EffectExecutor.<ExecuteAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.EffectExecutor.<ExecuteFlatAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.EffectExecutor.<ExecuteFlatAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__16>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__24>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.GameActions.<SettleResponseWindowAsync>d__22>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.GameActions.<SettleResponseWindowAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.GameCore.<EnforceHandLimitAsync>d__59>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.GameCore.<EnforceHandLimitAsync>d__59&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleAttackArrow.<FadeOutAsync>d__23>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleAttackArrow.<FadeOutAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleBoardAssemble.<PlayAsync>d__12>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleBoardAssemble.<PlayAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleFieldView.<HeroFatigueHitAsync>d__87>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleFieldView.<HeroFatigueHitAsync>d__87&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleFieldView.<HeroImpactPulseAsync>d__86>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleFieldView.<HeroImpactPulseAsync>d__86&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleFieldView.<WaitVisualsIdleAsync>d__72>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleFieldView.<WaitVisualsIdleAsync>d__72&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleStageDirector.<TweenLightX>d__31>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleStageDirector.<TweenLightX>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.CombatChoreographer.<DelayChainAware>d__62>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.CombatChoreographer.<DelayChainAware>d__62&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.CombatChoreographer.<WaitChainIdleAsync>d__63>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.CombatChoreographer.<WaitChainIdleAsync>d__63&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,CardCore.TideDelay.<After>d__0>(System.Runtime.CompilerServices.TaskAwaiter&,CardCore.TideDelay.<After>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.Nullable<byte>>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter<System.Nullable<byte>>,SynergyUI.BattleScreen.<ShowTipPromptAsync>d__110>(System.Runtime.CompilerServices.TaskAwaiter<System.Nullable<byte>>&,SynergyUI.BattleScreen.<ShowTipPromptAsync>d__110&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.Nullable<int>>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleScreen.<PickRegionOptionAsync>d__298>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleScreen.<PickRegionOptionAsync>d__298&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.HeroSkillSystem.<ActivateAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.HeroSkillSystem.<ActivateAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.CombatChoreographer.<WalkAsync>d__66>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.CombatChoreographer.<WalkAsync>d__66&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.ScriptedAi.<ExecuteAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.ScriptedAi.<ExecuteAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<PlayBestAffordableCardAsync>d__23>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<PlayBestAffordableCardAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<TryGraveyardPlayAsync>d__24>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<TryGraveyardPlayAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<UseHeroSkillAsync>d__20>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<UseHeroSkillAsync>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.CostCompensationService.<ExecutePayloadAtomAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.CostCompensationService.<ExecutePayloadAtomAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.CostCompensationService.<PayWithCompensationAsync>d__0>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.CostCompensationService.<PayWithCompensationAsync>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.GameActions.<ActivateHeroSkill>d__1>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.GameActions.<ActivateHeroSkill>d__1&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.ScriptedAi.<ExecuteAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.ScriptedAi.<ExecuteAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.SimpleAI.<UseHeroSkillAsync>d__20>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.SimpleAI.<UseHeroSkillAsync>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleFieldView.<DeadHoldAsync>d__83>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleFieldView.<DeadHoldAsync>d__83&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleFieldView.<SinkUnderBoardAsync>d__84>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleFieldView.<SinkUnderBoardAsync>d__84&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.CombatChoreographer.<WalkAsync>d__66>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.CombatChoreographer.<WalkAsync>d__66&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>,CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>&,CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<int>,CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter<int>&,CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.RegionTargetSelector.<SelectOneAsync>d__35>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.RegionTargetSelector.<SelectOneAsync>d__35&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>,CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>&,CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>,CardCore.TargetSelectionService.<RequestAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>&,CardCore.TargetSelectionService.<RequestAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>,SynergyUI.RegionTargetSelector.<Run>d__36>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>&,SynergyUI.RegionTargetSelector.<Run>d__36&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<int>,CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter<int>&,CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.EffectHandlerRegistry.<ResolveAtomTargetsAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.EffectHandlerRegistry.<ResolveAtomTargetsAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__8>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.EffectHandlerRegistry.<ResolveRewardTargetsAsync>d__11>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.EffectHandlerRegistry.<ResolveRewardTargetsAsync>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__12>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.TargetSelectionService.<RequestAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.TargetSelectionService.<RequestAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleScreen.<ShowResponsePopupAsync>d__294>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleScreen.<ShowResponsePopupAsync>d__294&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.RegionTargetSelector.<SelectAsync>d__33>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.RegionTargetSelector.<SelectAsync>d__33&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.RegionTargetSelector.<SelectIndicesAsync>d__34>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.RegionTargetSelector.<SelectIndicesAsync>d__34&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.CurseSystem.<ExecutePayloadAsync>d__13>(CardCore.CurseSystem.<ExecutePayloadAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.CurseSystem.<SettleAsync>d__12>(CardCore.CurseSystem.<SettleAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.DelayedEffectScheduler.<Fire>d__5>(CardCore.DelayedEffectScheduler.<Fire>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__21>(CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ApplyOutcomeRewardsAsync>d__22>(CardCore.EffectExecutor.<ApplyOutcomeRewardsAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ApplySituationGateAsync>d__23>(CardCore.EffectExecutor.<ApplySituationGateAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteAsync>d__13>(CardCore.EffectExecutor.<ExecuteAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteFlatAsync>d__14>(CardCore.EffectExecutor.<ExecuteFlatAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__16>(CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteStepsAsync>d__15>(CardCore.EffectExecutor.<ExecuteStepsAsync>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__24>(CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameActions.<ResolveCardCastAsync>d__10>(CardCore.GameActions.<ResolveCardCastAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameActions.<ResolveSpellEffectsAsync>d__25>(CardCore.GameActions.<ResolveSpellEffectsAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameActions.<SettleResponseWindowAsync>d__22>(CardCore.GameActions.<SettleResponseWindowAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameCore.<EnforceHandLimitAsync>d__59>(CardCore.GameCore.<EnforceHandLimitAsync>d__59&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameLoopController.<ProcessPriority>d__11>(CardCore.GameLoopController.<ProcessPriority>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameLoopController.<ResolveStack>d__12>(CardCore.GameLoopController.<ResolveStack>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.ProphecySystem.<ResolveAsync>d__10>(CardCore.ProphecySystem.<ResolveAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.StackEngine.<BeginResolution>d__50>(CardCore.StackEngine.<BeginResolution>d__50&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.StackEngine.<FinishResolution>d__52>(CardCore.StackEngine.<FinishResolution>d__52&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.StackEngine.<PlayerPass>d__47>(CardCore.StackEngine.<PlayerPass>d__47&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.StackEngine.<ResolveStack>d__51>(CardCore.StackEngine.<ResolveStack>d__51&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.StackEngine.<ResolveTop>d__58>(CardCore.StackEngine.<ResolveTop>d__58&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.TideDelay.<After>d__0>(CardCore.TideDelay.<After>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleAttackArrow.<FadeOutAsync>d__23>(SynergyUI.BattleAttackArrow.<FadeOutAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleBoardAssemble.<PlayAsync>d__12>(SynergyUI.BattleBoardAssemble.<PlayAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleCellPicker.<ConfirmAsync>d__14>(SynergyUI.BattleCellPicker.<ConfirmAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleController.<RunAiTurnAsync>d__25>(SynergyUI.BattleController.<RunAiTurnAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<HeroFatigueHitAsync>d__87>(SynergyUI.BattleFieldView.<HeroFatigueHitAsync>d__87&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<HeroImpactPulseAsync>d__86>(SynergyUI.BattleFieldView.<HeroImpactPulseAsync>d__86&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayDeathExitAsync>d__82>(SynergyUI.BattleFieldView.<PlayDeathExitAsync>d__82&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<WaitVisualsIdleAsync>d__72>(SynergyUI.BattleFieldView.<WaitVisualsIdleAsync>d__72&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleScreen.<<MakePacedAi>b__125_0>d>(SynergyUI.BattleScreen.<<MakePacedAi>b__125_0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleScreen.<<MakePacedScriptedAi>b__124_0>d>(SynergyUI.BattleScreen.<<MakePacedScriptedAi>b__124_0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleStageDirector.<TweenLightX>d__31>(SynergyUI.BattleStageDirector.<TweenLightX>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.CombatChoreographer.<ConfirmTargetAsync>d__39>(SynergyUI.CombatChoreographer.<ConfirmTargetAsync>d__39&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.CombatChoreographer.<DelayChainAware>d__62>(SynergyUI.CombatChoreographer.<DelayChainAware>d__62&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.CombatChoreographer.<ReturnAsync>d__59>(SynergyUI.CombatChoreographer.<ReturnAsync>d__59&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.CombatChoreographer.<WaitChainIdleAsync>d__63>(SynergyUI.CombatChoreographer.<WaitChainIdleAsync>d__63&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6>(SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.ScriptedAi.<TakeTurnAsync>d__9>(SynergyUI.ScriptedAi.<TakeTurnAsync>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.SimpleAI.<DoCombatAsync>d__37>(SynergyUI.SimpleAI.<DoCombatAsync>d__37&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.SimpleAI.<PaceAsync>d__12>(SynergyUI.SimpleAI.<PaceAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.SimpleAI.<StageCastAsync>d__14>(SynergyUI.SimpleAI.<StageCastAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.SimpleAI.<StageHoldAsync>d__13>(SynergyUI.SimpleAI.<StageHoldAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.SimpleAI.<TakeTurnAsync>d__18>(SynergyUI.SimpleAI.<TakeTurnAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.Nullable<byte>>.Start<SynergyUI.BattleScreen.<ShowTipPromptAsync>d__110>(SynergyUI.BattleScreen.<ShowTipPromptAsync>d__110&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.Nullable<int>>.Start<SynergyUI.BattleScreen.<PickRegionOptionAsync>d__298>(SynergyUI.BattleScreen.<PickRegionOptionAsync>d__298&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5>(CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CardCore.CostCompensationService.<ExecutePayloadAtomAsync>d__3>(CardCore.CostCompensationService.<ExecutePayloadAtomAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CardCore.CostCompensationService.<PayWithCompensationAsync>d__0>(CardCore.CostCompensationService.<PayWithCompensationAsync>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CardCore.GameActions.<ActivateHeroSkill>d__1>(CardCore.GameActions.<ActivateHeroSkill>d__1&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CardCore.HeroSkillSystem.<ActivateAsync>d__14>(CardCore.HeroSkillSystem.<ActivateAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.BattleFieldView.<DeadHoldAsync>d__83>(SynergyUI.BattleFieldView.<DeadHoldAsync>d__83&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.BattleFieldView.<SinkUnderBoardAsync>d__84>(SynergyUI.BattleFieldView.<SinkUnderBoardAsync>d__84&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.CombatChoreographer.<WalkAsync>d__66>(SynergyUI.CombatChoreographer.<WalkAsync>d__66&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.ScriptedAi.<ExecuteAsync>d__10>(SynergyUI.ScriptedAi.<ExecuteAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.SimpleAI.<PlayBestAffordableCardAsync>d__23>(SynergyUI.SimpleAI.<PlayBestAffordableCardAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.SimpleAI.<TryGraveyardPlayAsync>d__24>(SynergyUI.SimpleAI.<TryGraveyardPlayAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.SimpleAI.<UseHeroSkillAsync>d__20>(SynergyUI.SimpleAI.<UseHeroSkillAsync>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.Start<CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__13>(CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.Start<CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14>(CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.Start<SynergyUI.RegionTargetSelector.<SelectOneAsync>d__35>(SynergyUI.RegionTargetSelector.<SelectOneAsync>d__35&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.EffectHandlerRegistry.<ResolveAtomTargetsAsync>d__13>(CardCore.Attribute.EffectHandlerRegistry.<ResolveAtomTargetsAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__8>(CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.EffectHandlerRegistry.<ResolveRewardTargetsAsync>d__11>(CardCore.Attribute.EffectHandlerRegistry.<ResolveRewardTargetsAsync>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__12>(CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2>(CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12>(CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.TargetSelectionService.<RequestAsync>d__13>(CardCore.TargetSelectionService.<RequestAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<Cysharp.Threading.Tasks.UniTaskExtensions.<ContinueWith>d__60<object>>(Cysharp.Threading.Tasks.UniTaskExtensions.<ContinueWith>d__60<object>&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.BattleScreen.<ShowResponsePopupAsync>d__294>(SynergyUI.BattleScreen.<ShowResponsePopupAsync>d__294&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.RegionTargetSelector.<Run>d__36>(SynergyUI.RegionTargetSelector.<Run>d__36&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.RegionTargetSelector.<SelectAsync>d__33>(SynergyUI.RegionTargetSelector.<SelectAsync>d__33&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.RegionTargetSelector.<SelectIndicesAsync>d__34>(SynergyUI.RegionTargetSelector.<SelectIndicesAsync>d__34&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleCellPicker.<LoopAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleCellPicker.<LoopAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<LingerCastingAsync>d__38>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<LingerCastingAsync>d__38&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleHudRow.<>c__DisplayClass32_0.<<FlashPhase>b__0>d>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleHudRow.<>c__DisplayClass32_0.<<FlashPhase>b__0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleScreen.<FadeDisposeArrowAsync>d__137>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleScreen.<FadeDisposeArrowAsync>d__137&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleScreen.<NotChainCountdownAsync>d__296>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleScreen.<NotChainCountdownAsync>d__296&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleScreen.<PromptTutorialRetryAsync>d__141>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleScreen.<PromptTutorialRetryAsync>d__141&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleScreen.<ShowIntroPanelAfterCombatAsync>d__97>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleScreen.<ShowIntroPanelAfterCombatAsync>d__97&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleStageDirector.<MountBoardAndDawnAsync>d__25>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleStageDirector.<MountBoardAndDawnAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.CombatChoreographer.<PerformAsync>d__57>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.CombatChoreographer.<PerformAsync>d__57&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__38>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__38&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>,SynergyUI.BattleScreen.<OnConcede>d__293>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>&,SynergyUI.BattleScreen.<OnConcede>d__293&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>,SynergyUI.BattleScreen.<PromptTutorialCompleteAsync>d__143>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>&,SynergyUI.BattleScreen.<PromptTutorialCompleteAsync>d__143&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>,SynergyUI.BattleScreen.<PromptTutorialRetryAsync>d__141>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>&,SynergyUI.BattleScreen.<PromptTutorialRetryAsync>d__141&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>,SynergyUI.BattleScreen.<ActivateSkillAsync>d__290>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>&,SynergyUI.BattleScreen.<ActivateSkillAsync>d__290&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>,SynergyUI.BattleScreen.<OnClickUnitEffectButton>d__257>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>&,SynergyUI.BattleScreen.<OnClickUnitEffectButton>d__257&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>,SynergyUI.BattleScreen.<PickLandColorAsync>d__183>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>&,SynergyUI.BattleScreen.<PickLandColorAsync>d__183&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>,SynergyUI.BattleScreen.<PromptModeThenPickCell>d__195>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>&,SynergyUI.BattleScreen.<PromptModeThenPickCell>d__195&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>,SynergyUI.BattleScreen.<PromptModeThenPlay>d__178>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>&,SynergyUI.BattleScreen.<PromptModeThenPlay>d__178&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.BattleScreen.<ActivateSkillAsync>d__290>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.BattleScreen.<ActivateSkillAsync>d__290&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.CombatChoreographer.<PerformAsync>d__57>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.CombatChoreographer.<PerformAsync>d__57&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.GuardianRules.<SelectAndBindAsync>d__8>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.GuardianRules.<SelectAndBindAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.MartyrdomRules.<SelectAndBindAsync>d__7>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.MartyrdomRules.<SelectAndBindAsync>d__7&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleScreen.<DrainNetSelects>d__169>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleScreen.<DrainNetSelects>d__169&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleScreen.<OnGraveyardPlay>d__289>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleScreen.<OnGraveyardPlay>d__289&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleCellPicker.<LoopAsync>d__13>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleCellPicker.<LoopAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleFieldView.UnitVisual.<RollStatsAsync>d__27>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleFieldView.UnitVisual.<RollStatsAsync>d__27&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleScreen.<PlayTutorialVoiceAsync>d__113>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleScreen.<PlayTutorialVoiceAsync>d__113&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleScreen.<RefreshFieldWhenBoardReady>d__157>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleScreen.<RefreshFieldWhenBoardReady>d__157&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleScreen.<ShowIntroPanelAfterCombatAsync>d__97>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleScreen.<ShowIntroPanelAfterCombatAsync>d__97&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleScreen.<StartNetAsync>d__161>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleScreen.<StartNetAsync>d__161&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleStageDirector.<SwallowToBattleAsync>d__24>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleStageDirector.<SwallowToBattleAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleZoneProps.<PulseDeckAsync>d__22>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleZoneProps.<PulseDeckAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.CombatChoreographer.<PerformAsync>d__57>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.CombatChoreographer.<PerformAsync>d__57&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__38>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__38&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.CombatChoreographer.<TrackArrowsAsync>d__64>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.CombatChoreographer.<TrackArrowsAsync>d__64&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.PreMatchScreen.<FlipCoinAsync>d__21>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.PreMatchScreen.<FlipCoinAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<CardCore.Attribute.GuardianRules.<SelectAndBindAsync>d__8>(CardCore.Attribute.GuardianRules.<SelectAndBindAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<CardCore.Attribute.MartyrdomRules.<SelectAndBindAsync>d__7>(CardCore.Attribute.MartyrdomRules.<SelectAndBindAsync>d__7&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleCellPicker.<LoopAsync>d__13>(SynergyUI.BattleCellPicker.<LoopAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleFieldView.<LingerCastingAsync>d__38>(SynergyUI.BattleFieldView.<LingerCastingAsync>d__38&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleFieldView.UnitVisual.<RollStatsAsync>d__27>(SynergyUI.BattleFieldView.UnitVisual.<RollStatsAsync>d__27&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleHudRow.<>c__DisplayClass32_0.<<FlashPhase>b__0>d>(SynergyUI.BattleHudRow.<>c__DisplayClass32_0.<<FlashPhase>b__0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<ActivateSkillAsync>d__290>(SynergyUI.BattleScreen.<ActivateSkillAsync>d__290&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<DrainNetSelects>d__169>(SynergyUI.BattleScreen.<DrainNetSelects>d__169&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<FadeDisposeArrowAsync>d__137>(SynergyUI.BattleScreen.<FadeDisposeArrowAsync>d__137&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<NotChainCountdownAsync>d__296>(SynergyUI.BattleScreen.<NotChainCountdownAsync>d__296&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<OnClickUnitEffectButton>d__257>(SynergyUI.BattleScreen.<OnClickUnitEffectButton>d__257&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<OnConcede>d__293>(SynergyUI.BattleScreen.<OnConcede>d__293&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<OnGraveyardPlay>d__289>(SynergyUI.BattleScreen.<OnGraveyardPlay>d__289&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<PickLandColorAsync>d__183>(SynergyUI.BattleScreen.<PickLandColorAsync>d__183&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<PlayTutorialVoiceAsync>d__113>(SynergyUI.BattleScreen.<PlayTutorialVoiceAsync>d__113&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<PromptModeThenPickCell>d__195>(SynergyUI.BattleScreen.<PromptModeThenPickCell>d__195&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<PromptModeThenPlay>d__178>(SynergyUI.BattleScreen.<PromptModeThenPlay>d__178&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<PromptTutorialCompleteAsync>d__143>(SynergyUI.BattleScreen.<PromptTutorialCompleteAsync>d__143&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<PromptTutorialRetryAsync>d__141>(SynergyUI.BattleScreen.<PromptTutorialRetryAsync>d__141&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<RefreshFieldWhenBoardReady>d__157>(SynergyUI.BattleScreen.<RefreshFieldWhenBoardReady>d__157&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<ShowIntroPanelAfterCombatAsync>d__97>(SynergyUI.BattleScreen.<ShowIntroPanelAfterCombatAsync>d__97&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<StartNetAsync>d__161>(SynergyUI.BattleScreen.<StartNetAsync>d__161&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleStageDirector.<MountBoardAndDawnAsync>d__25>(SynergyUI.BattleStageDirector.<MountBoardAndDawnAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleStageDirector.<SwallowToBattleAsync>d__24>(SynergyUI.BattleStageDirector.<SwallowToBattleAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleZoneProps.<PulseDeckAsync>d__22>(SynergyUI.BattleZoneProps.<PulseDeckAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.CombatChoreographer.<PerformAsync>d__57>(SynergyUI.CombatChoreographer.<PerformAsync>d__57&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__38>(SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__38&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.CombatChoreographer.<TrackArrowsAsync>d__64>(SynergyUI.CombatChoreographer.<TrackArrowsAsync>d__64&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.PreMatchScreen.<FlipCoinAsync>d__21>(SynergyUI.PreMatchScreen.<FlipCoinAsync>d__21&)
		// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<int,object,object>> Cysharp.Threading.Tasks.UniTask.WhenAny<object,object>(Cysharp.Threading.Tasks.UniTask<object>,Cysharp.Threading.Tasks.UniTask<object>)
		// Cysharp.Threading.Tasks.UniTask<object> Cysharp.Threading.Tasks.UniTaskExtensions.ContinueWith<object>(Cysharp.Threading.Tasks.UniTask,System.Func<object>)
		// System.Void MemoryPack.IMemoryPackFormatter<object>.Serialize<object>(MemoryPack.MemoryPackWriter<object>&,object&)
		// byte[] MemoryPack.Internal.MemoryMarshalEx.AllocateUninitializedArray<byte>(int,bool)
		// int[] MemoryPack.Internal.MemoryMarshalEx.AllocateUninitializedArray<int>(int,bool)
		// object[] MemoryPack.Internal.MemoryMarshalEx.AllocateUninitializedArray<object>(int,bool)
		// uint[] MemoryPack.Internal.MemoryMarshalEx.AllocateUninitializedArray<uint>(int,bool)
		// byte& MemoryPack.Internal.MemoryMarshalEx.GetArrayDataReference<byte>(byte[])
		// int& MemoryPack.Internal.MemoryMarshalEx.GetArrayDataReference<int>(int[])
		// object& MemoryPack.Internal.MemoryMarshalEx.GetArrayDataReference<object>(object[])
		// uint& MemoryPack.Internal.MemoryMarshalEx.GetArrayDataReference<uint>(uint[])
		// MemoryPack.MemoryPackFormatter<object> MemoryPack.MemoryPackFormatterProvider.GetFormatter<object>()
		// bool MemoryPack.MemoryPackFormatterProvider.IsRegistered<System.Nullable<int>>()
		// bool MemoryPack.MemoryPackFormatterProvider.IsRegistered<int>()
		// bool MemoryPack.MemoryPackFormatterProvider.IsRegistered<object>()
		// System.Void MemoryPack.MemoryPackFormatterProvider.Register<System.Nullable<int>>(MemoryPack.MemoryPackFormatter<System.Nullable<int>>)
		// System.Void MemoryPack.MemoryPackFormatterProvider.Register<int>(MemoryPack.MemoryPackFormatter<int>)
		// System.Void MemoryPack.MemoryPackFormatterProvider.Register<object>(MemoryPack.MemoryPackFormatter<object>)
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanaged<System.Nullable<int>>(System.Nullable<int>&)
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanaged<int,System.Nullable<int>,System.Nullable<int>>(int&,System.Nullable<int>&,System.Nullable<int>&)
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<byte>(byte[]&)
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<int>(int[]&)
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<object>(object[]&)
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<uint>(uint[]&)
		// byte[] MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<byte>()
		// int[] MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<int>()
		// uint[] MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<uint>()
		// MemoryPack.IMemoryPackFormatter<object> MemoryPack.MemoryPackReader.GetFormatter<object>()
		// System.Void MemoryPack.MemoryPackReader.ReadArray<object>(object[]&)
		// object[] MemoryPack.MemoryPackReader.ReadArray<object>()
		// System.Void MemoryPack.MemoryPackReader.ReadPackable<object>(object&)
		// object MemoryPack.MemoryPackReader.ReadPackable<object>()
		// System.Void MemoryPack.MemoryPackReader.ReadPackableArray<object>(object[]&)
		// object[] MemoryPack.MemoryPackReader.ReadPackableArray<object>()
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<byte,int,int,int,int,int>(byte&,int&,int&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<byte,int,int>(byte&,int&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<byte>(byte&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<double>(double&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<float,int,int,int>(float&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<float,int>(float&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<float>(float&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,byte,uint>(int&,byte&,uint&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,byte>(int&,byte&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,float>(int&,float&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,int,int,int,byte,uint,int,int,byte,byte>(int&,int&,int&,int&,byte&,uint&,int&,int&,byte&,byte&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,int,int,int>(int&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,int>(int&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,uint,ushort,long>(int&,uint&,ushort&,long&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,uint>(int&,uint&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int>(int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<long>(long&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<uint,int>(uint&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<uint>(uint&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<ushort>(ushort&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanagedArray<byte>(byte[]&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanagedArray<int>(int[]&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanagedArray<uint>(uint[]&)
		// byte[] MemoryPack.MemoryPackReader.ReadUnmanagedArray<byte>()
		// int[] MemoryPack.MemoryPackReader.ReadUnmanagedArray<int>()
		// uint[] MemoryPack.MemoryPackReader.ReadUnmanagedArray<uint>()
		// System.Void MemoryPack.MemoryPackReader.ReadValue<object>(object&)
		// object MemoryPack.MemoryPackReader.ReadValue<object>()
		// int MemoryPack.MemoryPackSerializer.Deserialize<object>(System.ReadOnlySpan<byte>,object&,MemoryPack.MemoryPackSerializerOptions)
		// object MemoryPack.MemoryPackSerializer.Deserialize<object>(System.ReadOnlySpan<byte>,MemoryPack.MemoryPackSerializerOptions)
		// System.Void MemoryPack.MemoryPackSerializer.Serialize<object,object>(MemoryPack.MemoryPackWriter<object>&,object&)
		// byte[] MemoryPack.MemoryPackSerializer.Serialize<object>(object&,MemoryPack.MemoryPackSerializerOptions)
		// System.Void MemoryPack.MemoryPackWriter<object>.DangerousWriteUnmanaged<int,System.Nullable<int>,System.Nullable<int>>(int&,System.Nullable<int>&,System.Nullable<int>&)
		// System.Void MemoryPack.MemoryPackWriter<object>.DangerousWriteUnmanagedArray<byte>(byte[])
		// System.Void MemoryPack.MemoryPackWriter<object>.DangerousWriteUnmanagedArray<int>(int[])
		// System.Void MemoryPack.MemoryPackWriter<object>.DangerousWriteUnmanagedArray<object>(object[])
		// System.Void MemoryPack.MemoryPackWriter<object>.DangerousWriteUnmanagedArray<uint>(uint[])
		// MemoryPack.IMemoryPackFormatter<object> MemoryPack.MemoryPackWriter<object>.GetFormatter<object>()
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteArray<object>(object[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WritePackable<object>(object&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WritePackableArray<object>(object[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<byte,int,int,int,int,int>(byte&,int&,int&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<byte,int,int>(byte&,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<byte>(byte&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<double>(double&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<float,int,int,int>(float&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<float,int>(float&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,byte,uint>(int&,byte&,uint&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,byte>(int&,byte&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,int,int,int,byte,uint,int,int,byte,byte>(int&,int&,int&,int&,byte&,uint&,int&,int&,byte&,byte&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,int>(int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,uint>(int&,uint&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int>(int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<long>(long&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<uint>(uint&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedArray<byte>(byte[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedArray<int>(int[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedArray<uint>(uint[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<byte,int,int>(byte,byte&,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<byte>(byte,byte&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<float,int>(byte,float&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int,float>(byte,int&,float&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int,int,int,int>(byte,int&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int,int>(byte,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int,uint,ushort,long>(byte,int&,uint&,ushort&,long&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int,uint>(byte,int&,uint&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int>(byte,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<long>(byte,long&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<uint,int>(byte,uint&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteValue<object>(object&)
		// object Newtonsoft.Json.JsonConvert.DeserializeObject<object>(string,Newtonsoft.Json.JsonSerializerSettings)
		// object System.Activator.CreateInstance<object>()
		// System.ValueTuple<object,object>[] System.Array.Empty<System.ValueTuple<object,object>>()
		// byte[] System.Array.Empty<byte>()
		// float[] System.Array.Empty<float>()
		// int[] System.Array.Empty<int>()
		// object[] System.Array.Empty<object>()
		// uint[] System.Array.Empty<uint>()
		// int System.Array.IndexOf<int>(int[],int)
		// int System.Array.IndexOf<object>(object[],object)
		// int System.Array.IndexOfImpl<int>(int[],int,int,int)
		// int System.Array.IndexOfImpl<object>(object[],object,int,int)
		// float System.Collections.Generic.CollectionExtensions.GetValueOrDefault<int,float>(System.Collections.Generic.IReadOnlyDictionary<int,float>,int)
		// float System.Collections.Generic.CollectionExtensions.GetValueOrDefault<int,float>(System.Collections.Generic.IReadOnlyDictionary<int,float>,int,float)
		// int System.Collections.Generic.CollectionExtensions.GetValueOrDefault<object,int>(System.Collections.Generic.IReadOnlyDictionary<object,int>,object,int)
		// bool System.Enum.TryParse<int>(string,bool,int&)
		// bool System.Enum.TryParse<int>(string,int&)
		// bool System.Enum.TryParse<object>(string,bool,object&)
		// bool System.Enum.TryParse<object>(string,object&)
		// int System.HashCode.Combine<int,System.Nullable<int>>(int,System.Nullable<int>)
		// int System.HashCode.Combine<uint,System.DateTime>(uint,System.DateTime)
		// bool System.Linq.Enumerable.All<int>(System.Collections.Generic.IEnumerable<int>,System.Func<int,bool>)
		// bool System.Linq.Enumerable.All<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// bool System.Linq.Enumerable.Any<System.Collections.Generic.KeyValuePair<object,int>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>,System.Func<System.Collections.Generic.KeyValuePair<object,int>,bool>)
		// bool System.Linq.Enumerable.Any<int>(System.Collections.Generic.IEnumerable<int>)
		// bool System.Linq.Enumerable.Any<int>(System.Collections.Generic.IEnumerable<int>,System.Func<int,bool>)
		// bool System.Linq.Enumerable.Any<object>(System.Collections.Generic.IEnumerable<object>)
		// bool System.Linq.Enumerable.Any<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Cast<object>(System.Collections.IEnumerable)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.CastIterator<object>(System.Collections.IEnumerable)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Concat<object>(System.Collections.Generic.IEnumerable<object>,System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.ConcatIterator<object>(System.Collections.Generic.IEnumerable<object>,System.Collections.Generic.IEnumerable<object>)
		// bool System.Linq.Enumerable.Contains<object>(System.Collections.Generic.IEnumerable<object>,object)
		// bool System.Linq.Enumerable.Contains<object>(System.Collections.Generic.IEnumerable<object>,object,System.Collections.Generic.IEqualityComparer<object>)
		// int System.Linq.Enumerable.Count<System.Collections.Generic.KeyValuePair<int,object>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>)
		// int System.Linq.Enumerable.Count<System.ValueTuple<object,object>>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>)
		// int System.Linq.Enumerable.Count<object>(System.Collections.Generic.IEnumerable<object>)
		// int System.Linq.Enumerable.Count<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Distinct<int>(System.Collections.Generic.IEnumerable<int>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Distinct<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.DistinctIterator<int>(System.Collections.Generic.IEnumerable<int>,System.Collections.Generic.IEqualityComparer<int>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.DistinctIterator<object>(System.Collections.Generic.IEnumerable<object>,System.Collections.Generic.IEqualityComparer<object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Empty<object>()
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Except<int>(System.Collections.Generic.IEnumerable<int>,System.Collections.Generic.IEnumerable<int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.ExceptIterator<int>(System.Collections.Generic.IEnumerable<int>,System.Collections.Generic.IEnumerable<int>,System.Collections.Generic.IEqualityComparer<int>)
		// System.Collections.Generic.KeyValuePair<int,float> System.Linq.Enumerable.First<System.Collections.Generic.KeyValuePair<int,float>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>)
		// object System.Linq.Enumerable.First<object>(System.Collections.Generic.IEnumerable<object>)
		// System.ValueTuple<object,object,object> System.Linq.Enumerable.FirstOrDefault<System.ValueTuple<object,object,object>>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object,object>>,System.Func<System.ValueTuple<object,object,object>,bool>)
		// System.ValueTuple<object,object> System.Linq.Enumerable.FirstOrDefault<System.ValueTuple<object,object>>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>)
		// object System.Linq.Enumerable.FirstOrDefault<object>(System.Collections.Generic.IEnumerable<object>)
		// object System.Linq.Enumerable.FirstOrDefault<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// System.Collections.Generic.IEnumerable<System.Linq.IGrouping<System.ValueTuple<object,object>,System.ValueTuple<object,object>>> System.Linq.Enumerable.GroupBy<System.ValueTuple<object,object>,System.ValueTuple<object,object>>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>,System.Func<System.ValueTuple<object,object>,System.ValueTuple<object,object>>)
		// System.Collections.Generic.IEnumerable<System.Linq.IGrouping<int,System.Collections.Generic.KeyValuePair<int,object>>> System.Linq.Enumerable.GroupBy<System.Collections.Generic.KeyValuePair<int,object>,int>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>,System.Func<System.Collections.Generic.KeyValuePair<int,object>,int>)
		// System.Collections.Generic.IEnumerable<System.Linq.IGrouping<int,object>> System.Linq.Enumerable.GroupBy<object,int>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>)
		// System.Collections.Generic.IEnumerable<System.Linq.IGrouping<object,object>> System.Linq.Enumerable.GroupBy<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>)
		// double System.Linq.Enumerable.Last<double>(System.Collections.Generic.IEnumerable<double>)
		// object System.Linq.Enumerable.Last<object>(System.Collections.Generic.IEnumerable<object>)
		// object System.Linq.Enumerable.LastOrDefault<object>(System.Collections.Generic.IEnumerable<object>)
		// object System.Linq.Enumerable.LastOrDefault<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// float System.Linq.Enumerable.Max<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,float>)
		// float System.Linq.Enumerable.Min<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,float>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.OfType<object>(System.Collections.IEnumerable)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.OfTypeIterator<object>(System.Collections.IEnumerable)
		// System.Linq.IOrderedEnumerable<System.Collections.Generic.KeyValuePair<object,int>> System.Linq.Enumerable.OrderBy<System.Collections.Generic.KeyValuePair<object,int>,object>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>,System.Func<System.Collections.Generic.KeyValuePair<object,int>,object>,System.Collections.Generic.IComparer<object>)
		// System.Linq.IOrderedEnumerable<int> System.Linq.Enumerable.OrderBy<int,int>(System.Collections.Generic.IEnumerable<int>,System.Func<int,int>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,System.Guid>(System.Collections.Generic.IEnumerable<object>,System.Func<object,System.Guid>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,float>(System.Collections.Generic.IEnumerable<object>,System.Func<object,float>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,int>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>,System.Collections.Generic.IComparer<object>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,uint>(System.Collections.Generic.IEnumerable<object>,System.Func<object,uint>)
		// System.Linq.IOrderedEnumerable<System.Collections.Generic.KeyValuePair<int,float>> System.Linq.Enumerable.OrderByDescending<System.Collections.Generic.KeyValuePair<int,float>,float>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,float>)
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>> System.Linq.Enumerable.OrderByDescending<System.ValueTuple<int,float,float>,float>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,float,float>>,System.Func<System.ValueTuple<int,float,float>,float>)
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,int>> System.Linq.Enumerable.OrderByDescending<System.ValueTuple<int,int>,int>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,int>>,System.Func<System.ValueTuple<int,int>,int>)
		// System.Linq.IOrderedEnumerable<int> System.Linq.Enumerable.OrderByDescending<int,float>(System.Collections.Generic.IEnumerable<int>,System.Func<int,float>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderByDescending<object,byte>(System.Collections.Generic.IEnumerable<object>,System.Func<object,byte>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderByDescending<object,float>(System.Collections.Generic.IEnumerable<object>,System.Func<object,float>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderByDescending<object,int>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderByDescending<object,uint>(System.Collections.Generic.IEnumerable<object>,System.Func<object,uint>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Reverse<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.ReverseIterator<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>> System.Linq.Enumerable.Select<System.ValueTuple<object,object>,System.ValueTuple<object,object>>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>,System.Func<System.ValueTuple<object,object>,System.ValueTuple<object,object>>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>> System.Linq.Enumerable.Select<object,System.ValueTuple<object,object>>(System.Collections.Generic.IEnumerable<object>,System.Func<object,System.ValueTuple<object,object>>)
		// System.Collections.Generic.IEnumerable<UnityEngine.Vector4> System.Linq.Enumerable.Select<int,UnityEngine.Vector4>(System.Collections.Generic.IEnumerable<int>,System.Func<int,UnityEngine.Vector4>)
		// System.Collections.Generic.IEnumerable<byte> System.Linq.Enumerable.Select<object,byte>(System.Collections.Generic.IEnumerable<object>,System.Func<object,byte>)
		// System.Collections.Generic.IEnumerable<double> System.Linq.Enumerable.Select<float,double>(System.Collections.Generic.IEnumerable<float>,System.Func<float,double>)
		// System.Collections.Generic.IEnumerable<float> System.Linq.Enumerable.Select<float,float>(System.Collections.Generic.IEnumerable<float>,System.Func<float,float>)
		// System.Collections.Generic.IEnumerable<float> System.Linq.Enumerable.Select<object,float>(System.Collections.Generic.IEnumerable<object>,System.Func<object,float>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<int,int>,int>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Select<System.ValueTuple<int,int>,int>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,int>>,System.Func<System.ValueTuple<int,int>,int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Select<int,int>(System.Collections.Generic.IEnumerable<int>,System.Func<int,int>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<int,float>,object>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<int,int>,object>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<object,int>,object>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>,System.Func<System.Collections.Generic.KeyValuePair<object,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.ValueTuple<int,int>,object>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,int>>,System.Func<System.ValueTuple<int,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.ValueTuple<object,int>,object>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,int>>,System.Func<System.ValueTuple<object,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.ValueTuple<object,object>,object>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>,System.Func<System.ValueTuple<object,object>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<int,object>(System.Collections.Generic.IEnumerable<int>,System.Func<int,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>)
		// System.Collections.Generic.IEnumerable<uint> System.Linq.Enumerable.Select<object,uint>(System.Collections.Generic.IEnumerable<object>,System.Func<object,uint>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.SelectMany<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,System.Collections.Generic.IEnumerable<object>>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.SelectManyIterator<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,System.Collections.Generic.IEnumerable<object>>)
		// bool System.Linq.Enumerable.SequenceEqual<float>(System.Collections.Generic.IEnumerable<float>,System.Collections.Generic.IEnumerable<float>)
		// bool System.Linq.Enumerable.SequenceEqual<float>(System.Collections.Generic.IEnumerable<float>,System.Collections.Generic.IEnumerable<float>,System.Collections.Generic.IEqualityComparer<float>)
		// bool System.Linq.Enumerable.SequenceEqual<object>(System.Collections.Generic.IEnumerable<object>,System.Collections.Generic.IEnumerable<object>)
		// bool System.Linq.Enumerable.SequenceEqual<object>(System.Collections.Generic.IEnumerable<object>,System.Collections.Generic.IEnumerable<object>,System.Collections.Generic.IEqualityComparer<object>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Skip<int>(System.Collections.Generic.IEnumerable<int>,int)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Skip<object>(System.Collections.Generic.IEnumerable<object>,int)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.SkipIterator<int>(System.Collections.Generic.IEnumerable<int>,int)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.SkipIterator<object>(System.Collections.Generic.IEnumerable<object>,int)
		// float System.Linq.Enumerable.Sum<float>(System.Collections.Generic.IEnumerable<float>,System.Func<float,float>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Take<int>(System.Collections.Generic.IEnumerable<int>,int)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Take<object>(System.Collections.Generic.IEnumerable<object>,int)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.TakeIterator<int>(System.Collections.Generic.IEnumerable<int>,int)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.TakeIterator<object>(System.Collections.Generic.IEnumerable<object>,int)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.TakeLast<object>(System.Collections.Generic.IEnumerable<object>,int)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.TakeLastIterator<object>(System.Collections.Generic.IEnumerable<object>,int)
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>> System.Linq.Enumerable.ThenBy<System.ValueTuple<int,float,float>,int>(System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>>,System.Func<System.ValueTuple<int,float,float>,int>)
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,int>> System.Linq.Enumerable.ThenBy<System.ValueTuple<int,int>,int>(System.Linq.IOrderedEnumerable<System.ValueTuple<int,int>>,System.Func<System.ValueTuple<int,int>,int>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenBy<object,System.DateTime>(System.Linq.IOrderedEnumerable<object>,System.Func<object,System.DateTime>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenBy<object,byte>(System.Linq.IOrderedEnumerable<object>,System.Func<object,byte>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenBy<object,float>(System.Linq.IOrderedEnumerable<object>,System.Func<object,float>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenBy<object,int>(System.Linq.IOrderedEnumerable<object>,System.Func<object,int>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenBy<object,object>(System.Linq.IOrderedEnumerable<object>,System.Func<object,object>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenBy<object,object>(System.Linq.IOrderedEnumerable<object>,System.Func<object,object>,System.Collections.Generic.IComparer<object>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenBy<object,uint>(System.Linq.IOrderedEnumerable<object>,System.Func<object,uint>)
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>> System.Linq.Enumerable.ThenByDescending<System.ValueTuple<int,float,float>,float>(System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>>,System.Func<System.ValueTuple<int,float,float>,float>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenByDescending<object,System.DateTime>(System.Linq.IOrderedEnumerable<object>,System.Func<object,System.DateTime>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenByDescending<object,byte>(System.Linq.IOrderedEnumerable<object>,System.Func<object,byte>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenByDescending<object,int>(System.Linq.IOrderedEnumerable<object>,System.Func<object,int>)
		// System.ValueTuple<object,object>[] System.Linq.Enumerable.ToArray<System.ValueTuple<object,object>>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>)
		// UnityEngine.Vector4[] System.Linq.Enumerable.ToArray<UnityEngine.Vector4>(System.Collections.Generic.IEnumerable<UnityEngine.Vector4>)
		// int[] System.Linq.Enumerable.ToArray<int>(System.Collections.Generic.IEnumerable<int>)
		// object[] System.Linq.Enumerable.ToArray<object>(System.Collections.Generic.IEnumerable<object>)
		// uint[] System.Linq.Enumerable.ToArray<uint>(System.Collections.Generic.IEnumerable<uint>)
		// System.Collections.Generic.Dictionary<int,float> System.Linq.Enumerable.ToDictionary<System.Collections.Generic.KeyValuePair<int,int>,int,float>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,float>)
		// System.Collections.Generic.Dictionary<int,float> System.Linq.Enumerable.ToDictionary<System.Collections.Generic.KeyValuePair<int,int>,int,float>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,float>,System.Collections.Generic.IEqualityComparer<int>)
		// System.Collections.Generic.Dictionary<int,int> System.Linq.Enumerable.ToDictionary<System.Collections.Generic.KeyValuePair<int,int>,int,int>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>)
		// System.Collections.Generic.Dictionary<int,int> System.Linq.Enumerable.ToDictionary<System.Collections.Generic.KeyValuePair<int,int>,int,int>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>,System.Collections.Generic.IEqualityComparer<int>)
		// System.Collections.Generic.Dictionary<int,object> System.Linq.Enumerable.ToDictionary<System.Collections.Generic.KeyValuePair<int,object>,int,object>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>,System.Func<System.Collections.Generic.KeyValuePair<int,object>,int>,System.Func<System.Collections.Generic.KeyValuePair<int,object>,object>)
		// System.Collections.Generic.Dictionary<int,object> System.Linq.Enumerable.ToDictionary<System.Collections.Generic.KeyValuePair<int,object>,int,object>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>,System.Func<System.Collections.Generic.KeyValuePair<int,object>,int>,System.Func<System.Collections.Generic.KeyValuePair<int,object>,object>,System.Collections.Generic.IEqualityComparer<int>)
		// System.Collections.Generic.Dictionary<uint,object> System.Linq.Enumerable.ToDictionary<object,uint,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,uint>,System.Func<object,object>,System.Collections.Generic.IEqualityComparer<uint>)
		// System.Collections.Generic.Dictionary<uint,object> System.Linq.Enumerable.ToDictionary<object,uint>(System.Collections.Generic.IEnumerable<object>,System.Func<object,uint>)
		// System.Collections.Generic.HashSet<object> System.Linq.Enumerable.ToHashSet<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.HashSet<object> System.Linq.Enumerable.ToHashSet<object>(System.Collections.Generic.IEnumerable<object>,System.Collections.Generic.IEqualityComparer<object>)
		// System.Collections.Generic.List<System.ValueTuple<object,object>> System.Linq.Enumerable.ToList<System.ValueTuple<object,object>>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>)
		// System.Collections.Generic.List<float> System.Linq.Enumerable.ToList<float>(System.Collections.Generic.IEnumerable<float>)
		// System.Collections.Generic.List<int> System.Linq.Enumerable.ToList<int>(System.Collections.Generic.IEnumerable<int>)
		// System.Collections.Generic.List<object> System.Linq.Enumerable.ToList<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>> System.Linq.Enumerable.Where<System.Collections.Generic.KeyValuePair<int,float>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,bool>)
		// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>> System.Linq.Enumerable.Where<System.Collections.Generic.KeyValuePair<int,int>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,bool>)
		// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>> System.Linq.Enumerable.Where<System.Collections.Generic.KeyValuePair<int,object>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>,System.Func<System.Collections.Generic.KeyValuePair<int,object>,bool>)
		// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>> System.Linq.Enumerable.Where<System.Collections.Generic.KeyValuePair<object,int>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>,System.Func<System.Collections.Generic.KeyValuePair<object,int>,bool>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>> System.Linq.Enumerable.Where<System.ValueTuple<object,object>>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>,System.Func<System.ValueTuple<object,object>,bool>)
		// System.Collections.Generic.IEnumerable<float> System.Linq.Enumerable.Where<float>(System.Collections.Generic.IEnumerable<float>,System.Func<float,bool>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Where<int>(System.Collections.Generic.IEnumerable<int>,System.Func<int,bool>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Where<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Where<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int,bool>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.WhereIterator<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int,bool>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>> System.Linq.Enumerable.Iterator<System.ValueTuple<object,object>>.Select<System.ValueTuple<object,object>>(System.Func<System.ValueTuple<object,object>,System.ValueTuple<object,object>>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>> System.Linq.Enumerable.Iterator<object>.Select<System.ValueTuple<object,object>>(System.Func<object,System.ValueTuple<object,object>>)
		// System.Collections.Generic.IEnumerable<UnityEngine.Vector4> System.Linq.Enumerable.Iterator<int>.Select<UnityEngine.Vector4>(System.Func<int,UnityEngine.Vector4>)
		// System.Collections.Generic.IEnumerable<byte> System.Linq.Enumerable.Iterator<object>.Select<byte>(System.Func<object,byte>)
		// System.Collections.Generic.IEnumerable<double> System.Linq.Enumerable.Iterator<float>.Select<double>(System.Func<float,double>)
		// System.Collections.Generic.IEnumerable<float> System.Linq.Enumerable.Iterator<float>.Select<float>(System.Func<float,float>)
		// System.Collections.Generic.IEnumerable<float> System.Linq.Enumerable.Iterator<object>.Select<float>(System.Func<object,float>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,int>>.Select<int>(System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Iterator<System.ValueTuple<int,int>>.Select<int>(System.Func<System.ValueTuple<int,int>,int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Iterator<int>.Select<int>(System.Func<int,int>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,float>>.Select<object>(System.Func<System.Collections.Generic.KeyValuePair<int,float>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,int>>.Select<object>(System.Func<System.Collections.Generic.KeyValuePair<int,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<object,int>>.Select<object>(System.Func<System.Collections.Generic.KeyValuePair<object,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.ValueTuple<int,int>>.Select<object>(System.Func<System.ValueTuple<int,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.ValueTuple<object,int>>.Select<object>(System.Func<System.ValueTuple<object,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.ValueTuple<object,object>>.Select<object>(System.Func<System.ValueTuple<object,object>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<int>.Select<object>(System.Func<int,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<object>.Select<object>(System.Func<object,object>)
		// System.Collections.Generic.IEnumerable<uint> System.Linq.Enumerable.Iterator<object>.Select<uint>(System.Func<object,uint>)
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>> System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>>.CreateOrderedEnumerable<float>(System.Func<System.ValueTuple<int,float,float>,float>,System.Collections.Generic.IComparer<float>,bool)
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>> System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>>.CreateOrderedEnumerable<int>(System.Func<System.ValueTuple<int,float,float>,int>,System.Collections.Generic.IComparer<int>,bool)
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,int>> System.Linq.IOrderedEnumerable<System.ValueTuple<int,int>>.CreateOrderedEnumerable<int>(System.Func<System.ValueTuple<int,int>,int>,System.Collections.Generic.IComparer<int>,bool)
		// System.Linq.IOrderedEnumerable<object> System.Linq.IOrderedEnumerable<object>.CreateOrderedEnumerable<System.DateTime>(System.Func<object,System.DateTime>,System.Collections.Generic.IComparer<System.DateTime>,bool)
		// System.Linq.IOrderedEnumerable<object> System.Linq.IOrderedEnumerable<object>.CreateOrderedEnumerable<byte>(System.Func<object,byte>,System.Collections.Generic.IComparer<byte>,bool)
		// System.Linq.IOrderedEnumerable<object> System.Linq.IOrderedEnumerable<object>.CreateOrderedEnumerable<float>(System.Func<object,float>,System.Collections.Generic.IComparer<float>,bool)
		// System.Linq.IOrderedEnumerable<object> System.Linq.IOrderedEnumerable<object>.CreateOrderedEnumerable<int>(System.Func<object,int>,System.Collections.Generic.IComparer<int>,bool)
		// System.Linq.IOrderedEnumerable<object> System.Linq.IOrderedEnumerable<object>.CreateOrderedEnumerable<object>(System.Func<object,object>,System.Collections.Generic.IComparer<object>,bool)
		// System.Linq.IOrderedEnumerable<object> System.Linq.IOrderedEnumerable<object>.CreateOrderedEnumerable<uint>(System.Func<object,uint>,System.Collections.Generic.IComparer<uint>,bool)
		// System.Span<byte> System.MemoryExtensions.AsSpan<byte>(byte[])
		// System.Span<int> System.MemoryExtensions.AsSpan<int>(int[])
		// System.Span<object> System.MemoryExtensions.AsSpan<object>(object[])
		// System.Span<uint> System.MemoryExtensions.AsSpan<uint>(uint[])
		// object System.Reflection.CustomAttributeExtensions.GetCustomAttribute<object>(System.Reflection.MemberInfo)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleScreen.<OnEndTurn>d__292>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleScreen.<OnEndTurn>d__292&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleScreen.<OnEndTurnFan>d__123>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleScreen.<OnEndTurnFan>d__123&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleScreen.<OnEndTurnFan>d__123>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleScreen.<OnEndTurnFan>d__123&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.Start<SynergyUI.BattleScreen.<OnEndTurn>d__292>(SynergyUI.BattleScreen.<OnEndTurn>d__292&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.Start<SynergyUI.BattleScreen.<OnEndTurnFan>d__123>(SynergyUI.BattleScreen.<OnEndTurnFan>d__123&)
		// bool System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<object>()
		// byte& System.Runtime.CompilerServices.Unsafe.Add<byte>(byte&,int)
		// byte& System.Runtime.CompilerServices.Unsafe.As<byte,byte>(byte&)
		// byte& System.Runtime.CompilerServices.Unsafe.As<int,byte>(int&)
		// byte& System.Runtime.CompilerServices.Unsafe.As<object,byte>(object&)
		// byte& System.Runtime.CompilerServices.Unsafe.As<uint,byte>(uint&)
		// object& System.Runtime.CompilerServices.Unsafe.As<object,object>(object&)
		// System.Void* System.Runtime.CompilerServices.Unsafe.AsPointer<object>(object&)
		// object& System.Runtime.CompilerServices.Unsafe.AsRef<object>(object&)
		// System.Nullable<int> System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.Nullable<int>>(byte&)
		// byte System.Runtime.CompilerServices.Unsafe.ReadUnaligned<byte>(byte&)
		// double System.Runtime.CompilerServices.Unsafe.ReadUnaligned<double>(byte&)
		// float System.Runtime.CompilerServices.Unsafe.ReadUnaligned<float>(byte&)
		// int System.Runtime.CompilerServices.Unsafe.ReadUnaligned<int>(byte&)
		// long System.Runtime.CompilerServices.Unsafe.ReadUnaligned<long>(byte&)
		// object System.Runtime.CompilerServices.Unsafe.ReadUnaligned<object>(byte&)
		// uint System.Runtime.CompilerServices.Unsafe.ReadUnaligned<uint>(byte&)
		// ushort System.Runtime.CompilerServices.Unsafe.ReadUnaligned<ushort>(byte&)
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<System.Nullable<int>>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<byte>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<double>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<float>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<int>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<long>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<object>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<uint>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<ushort>()
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<System.Nullable<int>>(byte&,System.Nullable<int>)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<byte>(byte&,byte)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<double>(byte&,double)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<float>(byte&,float)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<int>(byte&,int)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<long>(byte&,long)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<object>(byte&,object)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<uint>(byte&,uint)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<ushort>(byte&,ushort)
		// byte& System.Runtime.InteropServices.MemoryMarshal.GetReference<byte>(System.ReadOnlySpan<byte>)
		// byte& System.Runtime.InteropServices.MemoryMarshal.GetReference<byte>(System.Span<byte>)
		// int& System.Runtime.InteropServices.MemoryMarshal.GetReference<int>(System.Span<int>)
		// object& System.Runtime.InteropServices.MemoryMarshal.GetReference<object>(System.Span<object>)
		// uint& System.Runtime.InteropServices.MemoryMarshal.GetReference<uint>(System.Span<uint>)
		// object& Unity.Collections.LowLevel.Unsafe.UnsafeUtility.As<object,object>(object&)
		// object UnityEngine.Component.GetComponent<object>()
		// object UnityEngine.Component.GetComponentInChildren<object>()
		// object UnityEngine.Component.GetComponentInChildren<object>(bool)
		// object UnityEngine.Component.GetComponentInParent<object>()
		// object[] UnityEngine.Component.GetComponents<object>()
		// object[] UnityEngine.Component.GetComponentsInChildren<object>()
		// object[] UnityEngine.Component.GetComponentsInChildren<object>(bool)
		// object UnityEngine.GameObject.AddComponent<object>()
		// object UnityEngine.GameObject.GetComponent<object>()
		// object UnityEngine.GameObject.GetComponentInParent<object>()
		// object UnityEngine.GameObject.GetComponentInParent<object>(bool)
		// object[] UnityEngine.GameObject.GetComponents<object>()
		// object[] UnityEngine.GameObject.GetComponentsInChildren<object>()
		// object[] UnityEngine.GameObject.GetComponentsInChildren<object>(bool)
		// System.Array UnityEngine.GameObject.GetComponentsInternal<object>(bool,bool,bool,bool,System.Collections.Generic.List<object>)
		// object UnityEngine.JsonUtility.FromJson<object>(string)
		// object UnityEngine.Object.FindFirstObjectByType<object>()
		// object[] UnityEngine.Object.FindObjectsByType<object>(UnityEngine.FindObjectsInactive,UnityEngine.FindObjectsSortMode)
		// object UnityEngine.Object.Instantiate<object>(object)
		// object UnityEngine.Object.Instantiate<object>(object,UnityEngine.Transform)
		// object UnityEngine.Object.Instantiate<object>(object,UnityEngine.Transform,bool)
		// object[] UnityEngine.Resources.ConvertObjects<object>(UnityEngine.Object[])
		// object UnityEngine.Resources.GetBuiltinResource<object>(string)
		// object UnityEngine.Resources.Load<object>(string)
		// System.Void UnityEngine.UIElements.CallbackEventHandler.AddEventCategories<object>(UnityEngine.UIElements.CallbackOptions)
		// System.Void UnityEngine.UIElements.CallbackEventHandler.RegisterCallback<object>(UnityEngine.UIElements.EventCallback<object>,UnityEngine.UIElements.TrickleDown)
		// System.Void UnityEngine.UIElements.EventCallbackRegistry.RegisterCallback<object>(UnityEngine.UIElements.EventCallback<object>,UnityEngine.UIElements.CallbackOptions)
		// bool UnityEngine.UIElements.INotifyValueChangedExtensions.RegisterValueChangedCallback<object>(UnityEngine.UIElements.INotifyValueChanged<object>,UnityEngine.UIElements.EventCallback<UnityEngine.UIElements.ChangeEvent<object>>)
		// object UnityEngine.UIElements.UQueryExtensions.Q<object>(UnityEngine.UIElements.VisualElement,string,string)
		// YooAsset.AssetHandle YooAsset.ResourcePackage.LoadAssetAsync<object>(string,uint)
		// YooAsset.SubAssetsHandle YooAsset.ResourcePackage.LoadSubAssetsAsync<object>(string,uint)
		// System.Collections.Generic.IReadOnlyList<object> YooAsset.SubAssetsHandle.GetSubAssetObjects<object>()
		// string string.Join<int>(string,System.Collections.Generic.IEnumerable<int>)
		// string string.JoinCore<int>(System.Char*,int,System.Collections.Generic.IEnumerable<int>)
	}
}