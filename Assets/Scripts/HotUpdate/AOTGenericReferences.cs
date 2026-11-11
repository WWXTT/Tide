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
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.EffectHandlerRegistry.<AskCompositionDeclarationAsync>d__23,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.EffectHandlerRegistry.<CollectDeclarationTargetsAsync>d__22,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.EffectHandlerRegistry.<ResolveAtomTargetsAsync>d__16,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__10,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.EffectHandlerRegistry.<ResolveRewardTargetsAsync>d__14,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__15,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.DeclareDeckHandler.<ExecuteAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.DeclareDeckTopHandler.<ExecuteAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.DiscardCardHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.MoveUnitHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.ProphecyHandlerUtil.<AskCardNameAsync>d__4,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.ProphecyHandlerUtil.<AskDimensionAsync>d__5,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.ProphecyHandlerUtil.<PickOptionAsync>d__6,System.ValueTuple<object,object,byte>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.ProphecyNextCardHandler.<ExecuteAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.RevealByDimensionHandler.<ExecuteAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.SearchDeckHandler.<ExecuteAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.CostCompensationService.<ExecutePayloadAtomAsync>d__3,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.CostCompensationService.<PayPayloadsAsync>d__0,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.CurseSystem.<>c__DisplayClass13_0.<<ExecutePayloadAsync>g__ExecuteAtomAsync|0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.CurseSystem.<ExecutePayloadAsync>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.CurseSystem.<SettleAsync>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.DelayedEffectScheduler.<Fire>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<>c__DisplayClass31_0.<<ExecuteReferencedEffectAsync>g__ExecuteAtomAsync|0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ApplyActiveEngineTrunkAsync>d__25>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ApplyActiveLuckTrunkAsync>d__24>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__21>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ApplyOutcomeRewardsAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ApplySituationGateAsync>d__23>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ApplyThenToSharedTargetsAsync>d__26>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteAsync>d__16>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteBranchRewardsAsync>d__30>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteFlatAsync>d__17>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteReferencedEffectAsync>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__19>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteStepsAsync>d__18>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__28>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<PayReplayRoundCostsAsync>d__39>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ResolveSegmentedTargetsAsync>d__20,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<ActivateHeroSkill>d__70,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<CollectSpellDeclarationTargetsAsync>d__41,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<EndTurnWithPhaseWindowAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<ForgetSafeAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<ResolveCardCastAsync>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<ResolveChargeDeclarationAsync>d__63>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<ResolveSpellEffectsAsync>d__42>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<SettleResponseWindowAsync>d__52>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameCore.<EnforceHandLimitAsync>d__65>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameLoopController.<ProcessPriority>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameLoopController.<ResolveStack>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Network.NetworkTargetSelector.<SelectAsync>d__18,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__19,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Network.NetworkTargetSelector.<SelectTextAsync>d__20,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.ProphecySystem.<ResolveAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.StackEngine.<BeginResolution>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.StackEngine.<FinishResolution>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.StackEngine.<PlayerPass>d__0>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.StackEngine.<ResolveStack>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.StackEngine.<ResolveTop>d__11>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.TargetSelectionService.<RequestAsync>d__22,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.TargetSelectionService.<RequestOneIndexAsync>d__23,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.TargetSelectionService.<RequestTextAsync>d__24,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.TideDelay.<After>d__0>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CloudSave.CloudSyncService.<RestoreFromCloudAsync>d__28,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CloudSave.CloudSyncService.<StartupSyncAsync>d__26>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CloudSave.CloudSyncService.<UploadAsync>d__27,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CloudSave.CloudTasks.<AwaitOnMain>d__0<object>,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CloudSave.CloudTasks.<AwaitOnMain>d__1>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CloudSave.TapAccountService.<EnsureLoginAsync>d__9,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleAttackArrow.<FadeOutAsync>d__23>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleBoardAssemble.<PlayAsync>d__9>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleCellPicker.<ConfirmAsync>d__14>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleCoinToss.<PlayAsync>d__14,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleController.<RunAiTurnAsync>d__29>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleDecisionHub.<ShowResponseAsync>d__60,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleDeckPickStage.<FlyToDeckAsync>d__32>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleDeckPickStage.<PickAsync>d__21,System.ValueTuple<object,object>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleEffectFx.<PlayOrbAsync>d__24>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<>c__DisplayClass103_0.<<PresentDeathByRefAsync>b__0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<>c__DisplayClass43_0.<<PresentEffectDeathAsync>b__0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<ArcMoveAsync>d__112>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<ClashFxCoreAsync>d__39>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<DamageFxCoreAsync>d__29>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<DamageRefCoreAsync>d__97>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<DeadHoldAsync>d__18,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<FlightFxCoreAsync>d__45>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<FlightRefCoreAsync>d__101>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<FloatTextAsync>d__47>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<HealFxCoreAsync>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<HealRefCoreAsync>d__99>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<HeroFatigueHitAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<HeroImpactPulseAsync>d__21>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayClashByRefAsync>d__108>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayClashResolvedAsync>d__37>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayCombatShotAsync>d__111>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayCounterFloatAsync>d__40>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayCounterFloatByRefAsync>d__104>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayDamageByRefAsync>d__96>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayDeathExitAsync>d__17>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayEffectDamageAsync>d__28>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayEffectFlightAsync>d__44>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayEffectHealAsync>d__30>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayFlightByRefAsync>d__100>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayGrantBurstAsync>d__35>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayGrantBurstByRefAsync>d__105>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayHealByRefAsync>d__98>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayLuckRollAsync>d__38>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PlayLuckRollByRefAsync>d__109>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PresentDeathByRefAsync>d__103>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<PresentEffectDeathAsync>d__43>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<RunUnitFxAsync>d__67>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<SinkUnderBoardAsync>d__19,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<TryRouteCastChainAsync>d__95,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<WaitCastPayArrived>d__9>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleFieldView.<WaitVisualsIdleAsync>d__65>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleOpeningDirector.<BeatAsync>d__41>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleOpeningDirector.<CoinStageAsync>d__31,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleOpeningDirector.<CoinStageForcedAsync>d__32,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleOpeningDirector.<DeckPickStageAsync>d__34,System.ValueTuple<object,object>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleOpeningDirector.<NetworkDeckPickAsync>d__33>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleOpeningDirector.<PlaceHeroesAsync>d__30>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleOpeningDirector.<WaitBoardAssembledAsync>d__29>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattlePlayhead.<RunReplayFrameAsync>d__39>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattlePlayhead.<WaitIdleAsync>d__43>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleResolutionDirector.<WaitIdleAsync>d__30>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleScreen.<<MakePacedAi>b__122_0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleScreen.<NetPickUnitCellAsync>d__157,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleScreen.<PickCellAsync>d__164,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleScreen.<PickMoveDestinationAsync>d__98,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleScreen.<PickRegionOptionAsync>d__107,System.Nullable<int>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleScreen.<ShowTipPromptAsync>d__267,System.Nullable<byte>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleStageDirector.<TweenLightX>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleTutorialDirector.<<MakePacedScriptedAi>b__57_0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleVfxFx.<TryPlayProjectileAsync>d__28,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.CombatChoreographer.<ConfirmTargetAsync>d__46>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.CombatChoreographer.<DelayChainAware>d__81>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.CombatChoreographer.<FlyAsync>d__85,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.CombatChoreographer.<ReturnAsync>d__78>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.CombatChoreographer.<WaitChainIdleAsync>d__82>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.DummyDeckPanel.<ShowAsync>d__13,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.DummyOptionsPanel.<ShowAsync>d__9,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.NetworkDeckPanel.<WaitAsync>d__15,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.Online.TapAccountService.<EnsureReadyAsync>d__17,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.Online.TapAccountService.<InitAndWaitAsync>d__21,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.Online.TapAccountService.<LoginAsync>d__18,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.Online.TapLobbyController.<CreateRoomAsync>d__26,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.Online.TapLobbyController.<EnsureLoginAndConnectAsync>d__18,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.Online.TapLobbyController.<GetRoomListAsync>d__27,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.Online.TapLobbyController.<JoinRoomAsync>d__28,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.Online.TapLobbyController.<LeaveAndDisconnectAsync>d__29>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.Online.TapLobbySection.<CreateRankedRoomAsync>d__14,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.Online.TapLobbySection.<CreateRoomAsync>d__13,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.Online.TapLobbySection.<CreateRoomCore>d__15,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.Online.TapLobbySection.<EnsureLoginAsync>d__10,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.Online.TapLobbySection.<JoinRoomAsync>d__16,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.Online.TapLobbySection.<LeaveAsync>d__17,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.Online.TapLobbySection.<ListRoomsAsync>d__11,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.RankedMatchmaking.<HostWaitingRoom>d__36>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.RankedMatchmaking.<JoinCandidate>d__35>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.RegionTargetSelector.<PromptTextAsync>d__56,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.RegionTargetSelector.<PromptTextModalAsync>d__58,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.RegionTargetSelector.<Run>d__59,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.RegionTargetSelector.<RunChoice>d__53,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.RegionTargetSelector.<RunTextInputAsync>d__57,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.RegionTargetSelector.<SelectAsync>d__48,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.RegionTargetSelector.<SelectIndicesAsync>d__50,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.RegionTargetSelector.<SelectOneAsync>d__54,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.RegionTargetSelector.<SelectTextAsync>d__55,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.ScriptedAi.<ExecuteAsync>d__13,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.ScriptedAi.<TakeTurnAsync>d__11>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<DoCombatAsync>d__41>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<PaceAsync>d__14>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<PlayBestAffordableCardAsync>d__28,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<StageCastAsync>d__16>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<StageHoldAsync>d__15>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<TakeTurnAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<UseHeroSkillAsync>d__25,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.TipPromptHost.<ShowAsync>d__5,System.Nullable<byte>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.EffectHandlerRegistry.<AskCompositionDeclarationAsync>d__23,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.EffectHandlerRegistry.<CollectDeclarationTargetsAsync>d__22,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.EffectHandlerRegistry.<ResolveAtomTargetsAsync>d__16,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__10,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.EffectHandlerRegistry.<ResolveRewardTargetsAsync>d__14,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__15,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.DeclareDeckHandler.<ExecuteAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.DeclareDeckTopHandler.<ExecuteAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.DiscardCardHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.MoveUnitHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.ProphecyHandlerUtil.<AskCardNameAsync>d__4,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.ProphecyHandlerUtil.<AskDimensionAsync>d__5,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.ProphecyHandlerUtil.<PickOptionAsync>d__6,System.ValueTuple<object,object,byte>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.ProphecyNextCardHandler.<ExecuteAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.RevealByDimensionHandler.<ExecuteAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.SearchDeckHandler.<ExecuteAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.CostCompensationService.<ExecutePayloadAtomAsync>d__3,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.CostCompensationService.<PayPayloadsAsync>d__0,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.CurseSystem.<>c__DisplayClass13_0.<<ExecutePayloadAsync>g__ExecuteAtomAsync|0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.CurseSystem.<ExecutePayloadAsync>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.CurseSystem.<SettleAsync>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.DelayedEffectScheduler.<Fire>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<>c__DisplayClass31_0.<<ExecuteReferencedEffectAsync>g__ExecuteAtomAsync|0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ApplyActiveEngineTrunkAsync>d__25>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ApplyActiveLuckTrunkAsync>d__24>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__21>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ApplyOutcomeRewardsAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ApplySituationGateAsync>d__23>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ApplyThenToSharedTargetsAsync>d__26>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteAsync>d__16>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteBranchRewardsAsync>d__30>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteFlatAsync>d__17>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteReferencedEffectAsync>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__19>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteStepsAsync>d__18>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__28>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<PayReplayRoundCostsAsync>d__39>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ResolveSegmentedTargetsAsync>d__20,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<ActivateHeroSkill>d__70,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<CollectSpellDeclarationTargetsAsync>d__41,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<EndTurnWithPhaseWindowAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<ForgetSafeAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<ResolveCardCastAsync>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<ResolveChargeDeclarationAsync>d__63>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<ResolveSpellEffectsAsync>d__42>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<SettleResponseWindowAsync>d__52>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameCore.<EnforceHandLimitAsync>d__65>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameLoopController.<ProcessPriority>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameLoopController.<ResolveStack>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Network.NetworkTargetSelector.<SelectAsync>d__18,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__19,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Network.NetworkTargetSelector.<SelectTextAsync>d__20,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.ProphecySystem.<ResolveAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.StackEngine.<BeginResolution>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.StackEngine.<FinishResolution>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.StackEngine.<PlayerPass>d__0>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.StackEngine.<ResolveStack>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.StackEngine.<ResolveTop>d__11>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.TargetSelectionService.<RequestAsync>d__22,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.TargetSelectionService.<RequestOneIndexAsync>d__23,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.TargetSelectionService.<RequestTextAsync>d__24,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.TideDelay.<After>d__0>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CloudSave.CloudSyncService.<RestoreFromCloudAsync>d__28,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CloudSave.CloudSyncService.<StartupSyncAsync>d__26>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CloudSave.CloudSyncService.<UploadAsync>d__27,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CloudSave.CloudTasks.<AwaitOnMain>d__0<object>,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CloudSave.CloudTasks.<AwaitOnMain>d__1>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CloudSave.TapAccountService.<EnsureLoginAsync>d__9,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleAttackArrow.<FadeOutAsync>d__23>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleBoardAssemble.<PlayAsync>d__9>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleCellPicker.<ConfirmAsync>d__14>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleCoinToss.<PlayAsync>d__14,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleController.<RunAiTurnAsync>d__29>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleDecisionHub.<ShowResponseAsync>d__60,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleDeckPickStage.<FlyToDeckAsync>d__32>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleDeckPickStage.<PickAsync>d__21,System.ValueTuple<object,object>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleEffectFx.<PlayOrbAsync>d__24>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<>c__DisplayClass103_0.<<PresentDeathByRefAsync>b__0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<>c__DisplayClass43_0.<<PresentEffectDeathAsync>b__0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<ArcMoveAsync>d__112>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<ClashFxCoreAsync>d__39>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<DamageFxCoreAsync>d__29>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<DamageRefCoreAsync>d__97>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<DeadHoldAsync>d__18,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<FlightFxCoreAsync>d__45>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<FlightRefCoreAsync>d__101>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<FloatTextAsync>d__47>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<HealFxCoreAsync>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<HealRefCoreAsync>d__99>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<HeroFatigueHitAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<HeroImpactPulseAsync>d__21>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayClashByRefAsync>d__108>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayClashResolvedAsync>d__37>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayCombatShotAsync>d__111>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayCounterFloatAsync>d__40>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayCounterFloatByRefAsync>d__104>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayDamageByRefAsync>d__96>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayDeathExitAsync>d__17>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayEffectDamageAsync>d__28>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayEffectFlightAsync>d__44>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayEffectHealAsync>d__30>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayFlightByRefAsync>d__100>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayGrantBurstAsync>d__35>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayGrantBurstByRefAsync>d__105>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayHealByRefAsync>d__98>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayLuckRollAsync>d__38>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PlayLuckRollByRefAsync>d__109>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PresentDeathByRefAsync>d__103>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<PresentEffectDeathAsync>d__43>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<RunUnitFxAsync>d__67>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<SinkUnderBoardAsync>d__19,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<TryRouteCastChainAsync>d__95,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<WaitCastPayArrived>d__9>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleFieldView.<WaitVisualsIdleAsync>d__65>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleOpeningDirector.<BeatAsync>d__41>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleOpeningDirector.<CoinStageAsync>d__31,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleOpeningDirector.<CoinStageForcedAsync>d__32,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleOpeningDirector.<DeckPickStageAsync>d__34,System.ValueTuple<object,object>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleOpeningDirector.<NetworkDeckPickAsync>d__33>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleOpeningDirector.<PlaceHeroesAsync>d__30>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleOpeningDirector.<WaitBoardAssembledAsync>d__29>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattlePlayhead.<RunReplayFrameAsync>d__39>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattlePlayhead.<WaitIdleAsync>d__43>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleResolutionDirector.<WaitIdleAsync>d__30>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleScreen.<<MakePacedAi>b__122_0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleScreen.<NetPickUnitCellAsync>d__157,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleScreen.<PickCellAsync>d__164,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleScreen.<PickMoveDestinationAsync>d__98,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleScreen.<PickRegionOptionAsync>d__107,System.Nullable<int>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleScreen.<ShowTipPromptAsync>d__267,System.Nullable<byte>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleStageDirector.<TweenLightX>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleTutorialDirector.<<MakePacedScriptedAi>b__57_0>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleVfxFx.<TryPlayProjectileAsync>d__28,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.CombatChoreographer.<ConfirmTargetAsync>d__46>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.CombatChoreographer.<DelayChainAware>d__81>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.CombatChoreographer.<FlyAsync>d__85,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.CombatChoreographer.<ReturnAsync>d__78>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.CombatChoreographer.<WaitChainIdleAsync>d__82>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.DummyDeckPanel.<ShowAsync>d__13,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.DummyOptionsPanel.<ShowAsync>d__9,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.NetworkDeckPanel.<WaitAsync>d__15,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.Online.TapAccountService.<EnsureReadyAsync>d__17,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.Online.TapAccountService.<InitAndWaitAsync>d__21,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.Online.TapAccountService.<LoginAsync>d__18,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.Online.TapLobbyController.<CreateRoomAsync>d__26,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.Online.TapLobbyController.<EnsureLoginAndConnectAsync>d__18,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.Online.TapLobbyController.<GetRoomListAsync>d__27,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.Online.TapLobbyController.<JoinRoomAsync>d__28,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.Online.TapLobbyController.<LeaveAndDisconnectAsync>d__29>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.Online.TapLobbySection.<CreateRankedRoomAsync>d__14,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.Online.TapLobbySection.<CreateRoomAsync>d__13,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.Online.TapLobbySection.<CreateRoomCore>d__15,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.Online.TapLobbySection.<EnsureLoginAsync>d__10,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.Online.TapLobbySection.<JoinRoomAsync>d__16,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.Online.TapLobbySection.<LeaveAsync>d__17,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.Online.TapLobbySection.<ListRoomsAsync>d__11,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.RankedMatchmaking.<HostWaitingRoom>d__36>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.RankedMatchmaking.<JoinCandidate>d__35>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.RegionTargetSelector.<PromptTextAsync>d__56,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.RegionTargetSelector.<PromptTextModalAsync>d__58,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.RegionTargetSelector.<Run>d__59,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.RegionTargetSelector.<RunChoice>d__53,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.RegionTargetSelector.<RunTextInputAsync>d__57,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.RegionTargetSelector.<SelectAsync>d__48,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.RegionTargetSelector.<SelectIndicesAsync>d__50,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.RegionTargetSelector.<SelectOneAsync>d__54,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.RegionTargetSelector.<SelectTextAsync>d__55,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.ScriptedAi.<ExecuteAsync>d__13,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.ScriptedAi.<TakeTurnAsync>d__11>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<DoCombatAsync>d__41>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<PaceAsync>d__14>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<PlayBestAffordableCardAsync>d__28,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<StageCastAsync>d__16>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<StageHoldAsync>d__15>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<TakeTurnAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<UseHeroSkillAsync>d__25,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.TipPromptHost.<ShowAsync>d__5,System.Nullable<byte>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.Nullable<byte>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.Nullable<int>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.ValueTuple<object,object,byte>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.ValueTuple<object,object>>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<CloudSave.CloudSyncService.CloudSyncHost.<TryAutoUpload>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<CloudSave.TapAccountService.<LoginFromUiAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<CloudSave.TapCloudSaveBootstrap.BootInitCallback.<RunAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleAiCastStage.<FadeDisposeArrowAsync>d__15>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleCellPicker.<LoopAsync>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleDecisionHub.<NotChainCountdownAsync>d__62>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleEffectFx.<LingerOrbAsync>d__25>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleEffectFx.Runner.<LifeAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleFieldView.<LingerCastingAsync>d__1>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleFieldView.UnitVisual.<RollStatsAsync>d__32>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleHudRow.<PlayPhaseRingAsync>d__48>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleOpeningDirector.<RunAsync>d__28>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattlePlayhead.<FinishAsync>d__40>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattlePlayhead.<RunPlaybackAsync>d__38>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleResolutionDirector.<FinishAsync>d__27>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleResolutionDirector.<RunReplayAsync>d__26>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<ActivateSkillAsync>d__105>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<NetActivateSkillAsync>d__168>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<NetAttackFlowAsync>d__166>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<NetEnsureFieldAppliedAsync>d__136>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<NetGuardFlowAsync>d__167>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<NetPlaceLandAsync>d__160>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<NetSelectAsync>d__173>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<NetTextAsync>d__175>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<NetUseCardAsync>d__156>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<OnClickUnitEffectButton>d__33>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<OnConcede>d__106>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<PickLandColorAsync>d__77>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<PromptModeThenPickCell>d__96>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<PulseAutoTargetGlowAsync>d__273>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<RefreshFieldWhenBoardReady>d__59>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<RejectDeckAndBackAsync>d__180>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<RunAiFirstTurnAsync>d__118>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<SpellDeclarationFlow>d__75>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<StartDummyOpeningAsync>d__115>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<StartGauntletOpeningAsync>d__113>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<StartReplayWhenBoardReadyAsync>d__185>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleStageDirector.<MountBoardAndDawnAsync>d__25>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleStageDirector.<SwallowToBattleAsync>d__24>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleTilePress.<RunAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleTutorialDirector.<AutoEndTurnAfterHealFxAsync>d__41>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleTutorialDirector.<ConsumeMidTransitionAsync>d__43>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleTutorialDirector.<PlayTutorialVoiceAsync>d__52>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleTutorialDirector.<PromptGauntletOverAsync>d__70>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleTutorialDirector.<PromptTutorialCompleteAsync>d__69>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleTutorialDirector.<PromptTutorialRetryAsync>d__68>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleTutorialDirector.<ShowIntroPanelAfterCombatAsync>d__46>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleVfxFx.<ReleaseSlotAfter>d__18>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleZoneProps.<PulseDeckAsync>d__28>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.CardComposerScreen.<ConfirmForceDeleteCardAsync>d__141>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.CombatChoreographer.<PerformAsync>d__76>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__45>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.CombatChoreographer.<TrackArrowsAsync>d__83>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.DeckBuilderScreen.<ConfirmCopyPresetAsync>d__115>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.DeckBuilderScreen.<ConfirmDeleteDeckAsync>d__117>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.EffectComposerScreen.<ConfirmForceDeleteEffectAsync>d__127>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.EffectComposerScreen.<HandleReplacedLegacyAsync>d__128>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.LaunchScreen.<AutoAdvance>d__18>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.LobbyScreen.<ConnectAsync>d__94>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.LobbyScreen.<CreateTapRoomAsync>d__93>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.LobbyScreen.<JoinTapEntryAsync>d__131>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.LobbyScreen.<RefreshTapRoomsAsync>d__129>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.LobbyScreen.<RunScanLoopAsync>d__123>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.Online.TapAccountService.<WarmupInternal>d__20>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.RankedMatchmaking.<RunLoop>d__33>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.RankedMatchmaking.<ScanOnce>d__34>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.TapLoginBadge.<LoginFlow>d__17>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<CloudSave.CloudSyncService.CloudSyncHost.<TryAutoUpload>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<CloudSave.TapAccountService.<LoginFromUiAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<CloudSave.TapCloudSaveBootstrap.BootInitCallback.<RunAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleAiCastStage.<FadeDisposeArrowAsync>d__15>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleCellPicker.<LoopAsync>d__13>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleDecisionHub.<NotChainCountdownAsync>d__62>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleEffectFx.<LingerOrbAsync>d__25>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleEffectFx.Runner.<LifeAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleFieldView.<LingerCastingAsync>d__1>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleFieldView.UnitVisual.<RollStatsAsync>d__32>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleHudRow.<PlayPhaseRingAsync>d__48>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleOpeningDirector.<RunAsync>d__28>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattlePlayhead.<FinishAsync>d__40>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattlePlayhead.<RunPlaybackAsync>d__38>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleResolutionDirector.<FinishAsync>d__27>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleResolutionDirector.<RunReplayAsync>d__26>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<ActivateSkillAsync>d__105>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<NetActivateSkillAsync>d__168>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<NetAttackFlowAsync>d__166>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<NetEnsureFieldAppliedAsync>d__136>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<NetGuardFlowAsync>d__167>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<NetPlaceLandAsync>d__160>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<NetSelectAsync>d__173>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<NetTextAsync>d__175>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<NetUseCardAsync>d__156>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<OnClickUnitEffectButton>d__33>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<OnConcede>d__106>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<PickLandColorAsync>d__77>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<PromptModeThenPickCell>d__96>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<PulseAutoTargetGlowAsync>d__273>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<RefreshFieldWhenBoardReady>d__59>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<RejectDeckAndBackAsync>d__180>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<RunAiFirstTurnAsync>d__118>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<SpellDeclarationFlow>d__75>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<StartDummyOpeningAsync>d__115>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<StartGauntletOpeningAsync>d__113>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<StartReplayWhenBoardReadyAsync>d__185>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleStageDirector.<MountBoardAndDawnAsync>d__25>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleStageDirector.<SwallowToBattleAsync>d__24>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleTilePress.<RunAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleTutorialDirector.<AutoEndTurnAfterHealFxAsync>d__41>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleTutorialDirector.<ConsumeMidTransitionAsync>d__43>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleTutorialDirector.<PlayTutorialVoiceAsync>d__52>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleTutorialDirector.<PromptGauntletOverAsync>d__70>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleTutorialDirector.<PromptTutorialCompleteAsync>d__69>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleTutorialDirector.<PromptTutorialRetryAsync>d__68>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleTutorialDirector.<ShowIntroPanelAfterCombatAsync>d__46>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleVfxFx.<ReleaseSlotAfter>d__18>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleZoneProps.<PulseDeckAsync>d__28>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.CardComposerScreen.<ConfirmForceDeleteCardAsync>d__141>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.CombatChoreographer.<PerformAsync>d__76>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__45>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.CombatChoreographer.<TrackArrowsAsync>d__83>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.DeckBuilderScreen.<ConfirmCopyPresetAsync>d__115>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.DeckBuilderScreen.<ConfirmDeleteDeckAsync>d__117>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.EffectComposerScreen.<ConfirmForceDeleteEffectAsync>d__127>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.EffectComposerScreen.<HandleReplacedLegacyAsync>d__128>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.LaunchScreen.<AutoAdvance>d__18>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.LobbyScreen.<ConnectAsync>d__94>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.LobbyScreen.<CreateTapRoomAsync>d__93>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.LobbyScreen.<JoinTapEntryAsync>d__131>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.LobbyScreen.<RefreshTapRoomsAsync>d__129>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.LobbyScreen.<RunScanLoopAsync>d__123>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.Online.TapAccountService.<WarmupInternal>d__20>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.RankedMatchmaking.<RunLoop>d__33>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.RankedMatchmaking.<ScanOnce>d__34>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.TapLoginBadge.<LoginFlow>d__17>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<System.Nullable<byte>>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<System.Nullable<int>>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<System.ValueTuple<object,object,byte>>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<System.ValueTuple<object,object>>
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
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,int>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<object,object,byte>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<object,object>>
	// Cysharp.Threading.Tasks.IUniTaskSource<byte>
	// Cysharp.Threading.Tasks.IUniTaskSource<int>
	// Cysharp.Threading.Tasks.IUniTaskSource<object>
	// Cysharp.Threading.Tasks.Internal.StatePool<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>>
	// Cysharp.Threading.Tasks.Internal.StateTuple<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>>
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
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,int>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<object,object,byte>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<object,object>>
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
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,int>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<object,object,byte>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<object,object>>
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
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,int>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<object,object,byte>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<object,object>>
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
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,int>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<object,object,byte>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<object,object>>
	// Cysharp.Threading.Tasks.UniTask<byte>
	// Cysharp.Threading.Tasks.UniTask<int>
	// Cysharp.Threading.Tasks.UniTask<object>
	// Cysharp.Threading.Tasks.UniTaskCompletionSource<int>
	// Cysharp.Threading.Tasks.UniTaskCompletionSource<object>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<Cysharp.Threading.Tasks.AsyncUnit>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<System.Nullable<byte>>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<System.Nullable<int>>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<System.ValueTuple<object,object,byte>>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<System.ValueTuple<object,object>>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<byte>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<int>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<object>
	// Cysharp.Threading.Tasks.UniTaskExtensions.<>c__32<object>
	// Cysharp.Threading.Tasks.UniTaskExtensions.<>c__51<byte>
	// MemoryPack.Formatters.ArrayFormatter<byte>
	// MemoryPack.Formatters.ArrayFormatter<float>
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
	// System.Action<CardCore.MatchLogLine>
	// System.Action<Cysharp.Threading.Tasks.UniTask>
	// System.Action<MemoryPack.Internal.BufferSegment>
	// System.Action<MonsterRig.ScatterSim.Piece>
	// System.Action<MonsterRig.WingBreathModule.Wing>
	// System.Action<SynergyUI.BattleCellPicker.CellOption>
	// System.Action<SynergyUI.BattleDeckPickStage.Entry>
	// System.Action<SynergyUI.BattleScreen.CellActions>
	// System.Action<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Action<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Action<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Action<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Action<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Action<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Action<System.ValueTuple<int,float,float>>
	// System.Action<System.ValueTuple<int,float>>
	// System.Action<System.ValueTuple<int,int>>
	// System.Action<System.ValueTuple<int,object,int>>
	// System.Action<System.ValueTuple<int,object>>
	// System.Action<System.ValueTuple<object,byte>>
	// System.Action<System.ValueTuple<object,int,UnityEngine.Color,int>>
	// System.Action<System.ValueTuple<object,int,int>>
	// System.Action<System.ValueTuple<object,int>>
	// System.Action<System.ValueTuple<object,object,byte>>
	// System.Action<System.ValueTuple<object,object,int>>
	// System.Action<System.ValueTuple<object,object,object>>
	// System.Action<System.ValueTuple<object,object>>
	// System.Action<Unity.InferenceEngine.Model.Input>
	// System.Action<UnityEngine.Color>
	// System.Action<UnityEngine.CombineInstance>
	// System.Action<UnityEngine.EventSystems.RaycastResult>
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
	// System.Action<object,System.Nullable<System.ValueTuple<int,int>>>
	// System.Action<object,object,int>
	// System.Action<object,object,object,object,System.Nullable<byte>,object,object>
	// System.Action<object,object,object,object,object>
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
	// System.Collections.Concurrent.ConcurrentQueue.<Enumerate>d__28<System.ValueTuple<object,object>>
	// System.Collections.Concurrent.ConcurrentQueue.<Enumerate>d__28<object>
	// System.Collections.Concurrent.ConcurrentQueue.Segment<System.ValueTuple<object,object>>
	// System.Collections.Concurrent.ConcurrentQueue.Segment<object>
	// System.Collections.Concurrent.ConcurrentQueue<System.ValueTuple<object,object>>
	// System.Collections.Concurrent.ConcurrentQueue<object>
	// System.Collections.Generic.ArraySortHelper<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.ArraySortHelper<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.ArraySortHelper<CardCore.MatchLogLine>
	// System.Collections.Generic.ArraySortHelper<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.ArraySortHelper<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.ArraySortHelper<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.ArraySortHelper<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.ArraySortHelper<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.ArraySortHelper<SynergyUI.BattleDeckPickStage.Entry>
	// System.Collections.Generic.ArraySortHelper<SynergyUI.BattleScreen.CellActions>
	// System.Collections.Generic.ArraySortHelper<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<int,float>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<int,int>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<int,object,int>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<int,object>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,byte>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,int,UnityEngine.Color,int>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,int,int>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,int>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,object,byte>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,object,int>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,object>>
	// System.Collections.Generic.ArraySortHelper<Unity.InferenceEngine.Model.Input>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.Color>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.CombineInstance>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.Quaternion>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.Vector2>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.Vector3>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.Vector4>
	// System.Collections.Generic.ArraySortHelper<byte>
	// System.Collections.Generic.ArraySortHelper<float>
	// System.Collections.Generic.ArraySortHelper<int>
	// System.Collections.Generic.ArraySortHelper<object>
	// System.Collections.Generic.ArraySortHelper<uint>
	// System.Collections.Generic.ArraySortHelper<ulong>
	// System.Collections.Generic.Comparer<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.Comparer<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.Comparer<CardCore.MatchLogLine>
	// System.Collections.Generic.Comparer<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.Comparer<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.Comparer<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.Comparer<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.Comparer<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.Comparer<SynergyUI.BattleDeckPickStage.Entry>
	// System.Collections.Generic.Comparer<SynergyUI.BattleScreen.CellActions>
	// System.Collections.Generic.Comparer<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.Comparer<System.DateTime>
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
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,int>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.Comparer<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.Comparer<System.ValueTuple<int,float>>
	// System.Collections.Generic.Comparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.Comparer<System.ValueTuple<int,object,int>>
	// System.Collections.Generic.Comparer<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.Comparer<System.ValueTuple<int,object>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,byte>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,int,UnityEngine.Color,int>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,int,int>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,object,byte>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,object,int>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.Comparer<Unity.InferenceEngine.Model.Input>
	// System.Collections.Generic.Comparer<UnityEngine.Color>
	// System.Collections.Generic.Comparer<UnityEngine.CombineInstance>
	// System.Collections.Generic.Comparer<UnityEngine.EventSystems.RaycastResult>
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
	// System.Collections.Generic.ComparisonComparer<CardCore.MatchLogLine>
	// System.Collections.Generic.ComparisonComparer<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.ComparisonComparer<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.ComparisonComparer<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.ComparisonComparer<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.ComparisonComparer<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.ComparisonComparer<SynergyUI.BattleDeckPickStage.Entry>
	// System.Collections.Generic.ComparisonComparer<SynergyUI.BattleScreen.CellActions>
	// System.Collections.Generic.ComparisonComparer<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.ComparisonComparer<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.ComparisonComparer<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ComparisonComparer<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ComparisonComparer<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.ComparisonComparer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ComparisonComparer<System.DateTime>
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
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,int>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<int,float>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<int,object,int>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<int,object>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<object,byte>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<object,int,UnityEngine.Color,int>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<object,int,int>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<object,object,byte>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<object,object,int>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.ComparisonComparer<Unity.InferenceEngine.Model.Input>
	// System.Collections.Generic.ComparisonComparer<UnityEngine.Color>
	// System.Collections.Generic.ComparisonComparer<UnityEngine.CombineInstance>
	// System.Collections.Generic.ComparisonComparer<UnityEngine.EventSystems.RaycastResult>
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
	// System.Collections.Generic.Dictionary.Enumerator<System.ValueTuple<object,int>,object>
	// System.Collections.Generic.Dictionary.Enumerator<byte,object>
	// System.Collections.Generic.Dictionary.Enumerator<int,System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.Dictionary.Enumerator<int,System.ValueTuple<object,int,int>>
	// System.Collections.Generic.Dictionary.Enumerator<int,UnityEngine.Color>
	// System.Collections.Generic.Dictionary.Enumerator<int,byte>
	// System.Collections.Generic.Dictionary.Enumerator<int,float>
	// System.Collections.Generic.Dictionary.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.Enumerator<int,long>
	// System.Collections.Generic.Dictionary.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.Enumerator<long,object>
	// System.Collections.Generic.Dictionary.Enumerator<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.Dictionary.Enumerator<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary.Enumerator<object,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.Enumerator<object,byte>
	// System.Collections.Generic.Dictionary.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.Enumerator<uint,System.ValueTuple<int,int>>
	// System.Collections.Generic.Dictionary.Enumerator<uint,System.ValueTuple<int,object>>
	// System.Collections.Generic.Dictionary.Enumerator<uint,UnityEngine.Vector2Int>
	// System.Collections.Generic.Dictionary.Enumerator<uint,int>
	// System.Collections.Generic.Dictionary.Enumerator<uint,object>
	// System.Collections.Generic.Dictionary.Enumerator<ulong,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<System.ValueTuple<int,int>,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<System.ValueTuple<object,int>,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<byte,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,System.ValueTuple<object,int,int>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,UnityEngine.Color>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,byte>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,float>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,long>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<long,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,byte>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<uint,System.ValueTuple<int,int>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<uint,System.ValueTuple<int,object>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<uint,UnityEngine.Vector2Int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<uint,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<uint,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<ulong,int>
	// System.Collections.Generic.Dictionary.KeyCollection<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary.KeyCollection<System.ValueTuple<int,int>,object>
	// System.Collections.Generic.Dictionary.KeyCollection<System.ValueTuple<object,int>,object>
	// System.Collections.Generic.Dictionary.KeyCollection<byte,object>
	// System.Collections.Generic.Dictionary.KeyCollection<int,System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.Dictionary.KeyCollection<int,System.ValueTuple<object,int,int>>
	// System.Collections.Generic.Dictionary.KeyCollection<int,UnityEngine.Color>
	// System.Collections.Generic.Dictionary.KeyCollection<int,byte>
	// System.Collections.Generic.Dictionary.KeyCollection<int,float>
	// System.Collections.Generic.Dictionary.KeyCollection<int,int>
	// System.Collections.Generic.Dictionary.KeyCollection<int,long>
	// System.Collections.Generic.Dictionary.KeyCollection<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection<long,object>
	// System.Collections.Generic.Dictionary.KeyCollection<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.Dictionary.KeyCollection<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary.KeyCollection<object,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.KeyCollection<object,byte>
	// System.Collections.Generic.Dictionary.KeyCollection<object,int>
	// System.Collections.Generic.Dictionary.KeyCollection<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection<uint,System.ValueTuple<int,int>>
	// System.Collections.Generic.Dictionary.KeyCollection<uint,System.ValueTuple<int,object>>
	// System.Collections.Generic.Dictionary.KeyCollection<uint,UnityEngine.Vector2Int>
	// System.Collections.Generic.Dictionary.KeyCollection<uint,int>
	// System.Collections.Generic.Dictionary.KeyCollection<uint,object>
	// System.Collections.Generic.Dictionary.KeyCollection<ulong,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<System.ValueTuple<int,int>,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<System.ValueTuple<object,int>,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<byte,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,System.ValueTuple<object,int,int>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,UnityEngine.Color>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,byte>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,float>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,long>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<long,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,byte>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<uint,System.ValueTuple<int,int>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<uint,System.ValueTuple<int,object>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<uint,UnityEngine.Vector2Int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<uint,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<uint,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<ulong,int>
	// System.Collections.Generic.Dictionary.ValueCollection<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary.ValueCollection<System.ValueTuple<int,int>,object>
	// System.Collections.Generic.Dictionary.ValueCollection<System.ValueTuple<object,int>,object>
	// System.Collections.Generic.Dictionary.ValueCollection<byte,object>
	// System.Collections.Generic.Dictionary.ValueCollection<int,System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.Dictionary.ValueCollection<int,System.ValueTuple<object,int,int>>
	// System.Collections.Generic.Dictionary.ValueCollection<int,UnityEngine.Color>
	// System.Collections.Generic.Dictionary.ValueCollection<int,byte>
	// System.Collections.Generic.Dictionary.ValueCollection<int,float>
	// System.Collections.Generic.Dictionary.ValueCollection<int,int>
	// System.Collections.Generic.Dictionary.ValueCollection<int,long>
	// System.Collections.Generic.Dictionary.ValueCollection<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection<long,object>
	// System.Collections.Generic.Dictionary.ValueCollection<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.Dictionary.ValueCollection<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary.ValueCollection<object,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.ValueCollection<object,byte>
	// System.Collections.Generic.Dictionary.ValueCollection<object,int>
	// System.Collections.Generic.Dictionary.ValueCollection<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection<uint,System.ValueTuple<int,int>>
	// System.Collections.Generic.Dictionary.ValueCollection<uint,System.ValueTuple<int,object>>
	// System.Collections.Generic.Dictionary.ValueCollection<uint,UnityEngine.Vector2Int>
	// System.Collections.Generic.Dictionary.ValueCollection<uint,int>
	// System.Collections.Generic.Dictionary.ValueCollection<uint,object>
	// System.Collections.Generic.Dictionary.ValueCollection<ulong,int>
	// System.Collections.Generic.Dictionary<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary<System.ValueTuple<int,int>,object>
	// System.Collections.Generic.Dictionary<System.ValueTuple<object,int>,object>
	// System.Collections.Generic.Dictionary<byte,object>
	// System.Collections.Generic.Dictionary<int,System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.Dictionary<int,System.ValueTuple<object,int,int>>
	// System.Collections.Generic.Dictionary<int,UnityEngine.Color>
	// System.Collections.Generic.Dictionary<int,byte>
	// System.Collections.Generic.Dictionary<int,float>
	// System.Collections.Generic.Dictionary<int,int>
	// System.Collections.Generic.Dictionary<int,long>
	// System.Collections.Generic.Dictionary<int,object>
	// System.Collections.Generic.Dictionary<long,object>
	// System.Collections.Generic.Dictionary<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.Dictionary<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary<object,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary<object,byte>
	// System.Collections.Generic.Dictionary<object,int>
	// System.Collections.Generic.Dictionary<object,object>
	// System.Collections.Generic.Dictionary<uint,System.ValueTuple<int,int>>
	// System.Collections.Generic.Dictionary<uint,System.ValueTuple<int,object>>
	// System.Collections.Generic.Dictionary<uint,UnityEngine.Vector2Int>
	// System.Collections.Generic.Dictionary<uint,int>
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
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,int>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<int,byte>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<int,object>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<object,int,int>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<object,object,byte>>
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
	// System.Collections.Generic.ICollection<CardCore.MatchLogLine>
	// System.Collections.Generic.ICollection<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.ICollection<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.ICollection<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.ICollection<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.ICollection<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.ICollection<SynergyUI.BattleDeckPickStage.Entry>
	// System.Collections.Generic.ICollection<SynergyUI.BattleScreen.CellActions>
	// System.Collections.Generic.ICollection<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,byte>,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<System.ValueTuple<object,int>,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<byte,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,System.ValueTuple<UnityEngine.Color,byte>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,System.ValueTuple<object,int,int>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,UnityEngine.Color>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,byte>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,long>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<long,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,int>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,object>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,int>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,object>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint,UnityEngine.Vector2Int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint,int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<ulong,int>>
	// System.Collections.Generic.ICollection<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.ICollection<System.ValueTuple<int,float>>
	// System.Collections.Generic.ICollection<System.ValueTuple<int,int>>
	// System.Collections.Generic.ICollection<System.ValueTuple<int,object,int>>
	// System.Collections.Generic.ICollection<System.ValueTuple<int,object>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,byte>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,int,UnityEngine.Color,int>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,int,int>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,int>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,object,byte>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,object,int>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,object>>
	// System.Collections.Generic.ICollection<Unity.InferenceEngine.Model.Input>
	// System.Collections.Generic.ICollection<UnityEngine.Color>
	// System.Collections.Generic.ICollection<UnityEngine.CombineInstance>
	// System.Collections.Generic.ICollection<UnityEngine.EventSystems.RaycastResult>
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
	// System.Collections.Generic.IComparer<CardCore.MatchLogLine>
	// System.Collections.Generic.IComparer<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.IComparer<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.IComparer<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.IComparer<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.IComparer<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.IComparer<SynergyUI.BattleDeckPickStage.Entry>
	// System.Collections.Generic.IComparer<SynergyUI.BattleScreen.CellActions>
	// System.Collections.Generic.IComparer<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IComparer<System.DateTime>
	// System.Collections.Generic.IComparer<System.Guid>
	// System.Collections.Generic.IComparer<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.IComparer<System.ValueTuple<int,float>>
	// System.Collections.Generic.IComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.IComparer<System.ValueTuple<int,object,int>>
	// System.Collections.Generic.IComparer<System.ValueTuple<int,object>>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,byte>>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,int,UnityEngine.Color,int>>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,int,int>>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,object,byte>>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,object,int>>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.IComparer<Unity.InferenceEngine.Model.Input>
	// System.Collections.Generic.IComparer<UnityEngine.Color>
	// System.Collections.Generic.IComparer<UnityEngine.CombineInstance>
	// System.Collections.Generic.IComparer<UnityEngine.EventSystems.RaycastResult>
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
	// System.Collections.Generic.IEnumerable<CardCore.MatchLogLine>
	// System.Collections.Generic.IEnumerable<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.IEnumerable<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.IEnumerable<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.IEnumerable<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.IEnumerable<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.IEnumerable<SynergyUI.BattleDeckPickStage.Entry>
	// System.Collections.Generic.IEnumerable<SynergyUI.BattleScreen.CellActions>
	// System.Collections.Generic.IEnumerable<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,byte>,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<System.ValueTuple<object,int>,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<byte,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,System.ValueTuple<UnityEngine.Color,byte>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,System.ValueTuple<object,int,int>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,UnityEngine.Color>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,byte>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,long>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<long,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,int>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,object>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,int>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,object>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<uint,UnityEngine.Vector2Int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<uint,int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<uint,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<ulong,int>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<int,float>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<int,int>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<int,object,int>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<int,object>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,byte>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,int,UnityEngine.Color,int>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,int,int>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,int>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object,byte>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object,int>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>
	// System.Collections.Generic.IEnumerable<Unity.InferenceEngine.Model.Input>
	// System.Collections.Generic.IEnumerable<UnityEngine.Color>
	// System.Collections.Generic.IEnumerable<UnityEngine.CombineInstance>
	// System.Collections.Generic.IEnumerable<UnityEngine.EventSystems.RaycastResult>
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
	// System.Collections.Generic.IEnumerable<ushort>
	// System.Collections.Generic.IEnumerator<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.IEnumerator<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.IEnumerator<CardCore.MatchLogLine>
	// System.Collections.Generic.IEnumerator<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.IEnumerator<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.IEnumerator<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.IEnumerator<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.IEnumerator<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.IEnumerator<SynergyUI.BattleDeckPickStage.Entry>
	// System.Collections.Generic.IEnumerator<SynergyUI.BattleScreen.CellActions>
	// System.Collections.Generic.IEnumerator<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,byte>,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<System.ValueTuple<object,int>,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<byte,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,System.ValueTuple<UnityEngine.Color,byte>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,System.ValueTuple<object,int,int>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,UnityEngine.Color>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,byte>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,long>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<long,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,int>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,object>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,int>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,object>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<uint,UnityEngine.Vector2Int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<uint,int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<uint,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<ulong,int>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<int,float>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<int,int>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<int,object,int>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<int,object>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,byte>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,int,UnityEngine.Color,int>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,int,int>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,int>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,object,byte>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,object,int>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,object>>
	// System.Collections.Generic.IEnumerator<Unity.InferenceEngine.Model.Input>
	// System.Collections.Generic.IEnumerator<UnityEngine.Color>
	// System.Collections.Generic.IEnumerator<UnityEngine.CombineInstance>
	// System.Collections.Generic.IEnumerator<UnityEngine.EventSystems.RaycastResult>
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
	// System.Collections.Generic.IEnumerator<ushort>
	// System.Collections.Generic.IEqualityComparer<System.ValueTuple<int,byte>>
	// System.Collections.Generic.IEqualityComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.IEqualityComparer<System.ValueTuple<object,int>>
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
	// System.Collections.Generic.IList<CardCore.MatchLogLine>
	// System.Collections.Generic.IList<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.IList<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.IList<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.IList<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.IList<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.IList<SynergyUI.BattleDeckPickStage.Entry>
	// System.Collections.Generic.IList<SynergyUI.BattleScreen.CellActions>
	// System.Collections.Generic.IList<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IList<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.IList<System.ValueTuple<int,float>>
	// System.Collections.Generic.IList<System.ValueTuple<int,int>>
	// System.Collections.Generic.IList<System.ValueTuple<int,object,int>>
	// System.Collections.Generic.IList<System.ValueTuple<int,object>>
	// System.Collections.Generic.IList<System.ValueTuple<object,byte>>
	// System.Collections.Generic.IList<System.ValueTuple<object,int,UnityEngine.Color,int>>
	// System.Collections.Generic.IList<System.ValueTuple<object,int,int>>
	// System.Collections.Generic.IList<System.ValueTuple<object,int>>
	// System.Collections.Generic.IList<System.ValueTuple<object,object,byte>>
	// System.Collections.Generic.IList<System.ValueTuple<object,object,int>>
	// System.Collections.Generic.IList<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.IList<System.ValueTuple<object,object>>
	// System.Collections.Generic.IList<Unity.InferenceEngine.Model.Input>
	// System.Collections.Generic.IList<UnityEngine.Color>
	// System.Collections.Generic.IList<UnityEngine.CombineInstance>
	// System.Collections.Generic.IList<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.Generic.IList<UnityEngine.Quaternion>
	// System.Collections.Generic.IList<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.IList<UnityEngine.Vector2>
	// System.Collections.Generic.IList<UnityEngine.Vector3>
	// System.Collections.Generic.IList<UnityEngine.Vector4>
	// System.Collections.Generic.IList<byte>
	// System.Collections.Generic.IList<double>
	// System.Collections.Generic.IList<float>
	// System.Collections.Generic.IList<int>
	// System.Collections.Generic.IList<object>
	// System.Collections.Generic.IList<uint>
	// System.Collections.Generic.IList<ulong>
	// System.Collections.Generic.IReadOnlyCollection<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.IReadOnlyCollection<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.IReadOnlyCollection<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IReadOnlyCollection<System.Nullable<System.ValueTuple<uint,int,int>>>
	// System.Collections.Generic.IReadOnlyCollection<System.ValueTuple<int,int>>
	// System.Collections.Generic.IReadOnlyCollection<UnityEngine.Vector3>
	// System.Collections.Generic.IReadOnlyCollection<object>
	// System.Collections.Generic.IReadOnlyDictionary<int,float>
	// System.Collections.Generic.IReadOnlyDictionary<object,int>
	// System.Collections.Generic.IReadOnlyDictionary<object,object>
	// System.Collections.Generic.IReadOnlyList<System.Nullable<System.ValueTuple<uint,int,int>>>
	// System.Collections.Generic.IReadOnlyList<System.ValueTuple<int,int>>
	// System.Collections.Generic.IReadOnlyList<UnityEngine.Vector3>
	// System.Collections.Generic.IReadOnlyList<object>
	// System.Collections.Generic.KeyValuePair<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.KeyValuePair<System.ValueTuple<int,int>,object>
	// System.Collections.Generic.KeyValuePair<System.ValueTuple<object,int>,object>
	// System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>
	// System.Collections.Generic.KeyValuePair<byte,object>
	// System.Collections.Generic.KeyValuePair<int,System.ValueTuple<UnityEngine.Color,byte>>
	// System.Collections.Generic.KeyValuePair<int,System.ValueTuple<object,int,int>>
	// System.Collections.Generic.KeyValuePair<int,UnityEngine.Color>
	// System.Collections.Generic.KeyValuePair<int,byte>
	// System.Collections.Generic.KeyValuePair<int,float>
	// System.Collections.Generic.KeyValuePair<int,int>
	// System.Collections.Generic.KeyValuePair<int,long>
	// System.Collections.Generic.KeyValuePair<int,object>
	// System.Collections.Generic.KeyValuePair<long,object>
	// System.Collections.Generic.KeyValuePair<object,System.Collections.Generic.KeyValuePair<UnityEngine.Vector3,UnityEngine.Quaternion>>
	// System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,object>>
	// System.Collections.Generic.KeyValuePair<object,byte>
	// System.Collections.Generic.KeyValuePair<object,int>
	// System.Collections.Generic.KeyValuePair<object,object>
	// System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,int>>
	// System.Collections.Generic.KeyValuePair<uint,System.ValueTuple<int,object>>
	// System.Collections.Generic.KeyValuePair<uint,UnityEngine.Vector2Int>
	// System.Collections.Generic.KeyValuePair<uint,int>
	// System.Collections.Generic.KeyValuePair<uint,object>
	// System.Collections.Generic.KeyValuePair<ulong,int>
	// System.Collections.Generic.List.Enumerator<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.List.Enumerator<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.List.Enumerator<CardCore.MatchLogLine>
	// System.Collections.Generic.List.Enumerator<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.List.Enumerator<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.List.Enumerator<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.List.Enumerator<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.List.Enumerator<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.List.Enumerator<SynergyUI.BattleDeckPickStage.Entry>
	// System.Collections.Generic.List.Enumerator<SynergyUI.BattleScreen.CellActions>
	// System.Collections.Generic.List.Enumerator<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<int,float>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<int,int>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<int,object,int>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<int,object>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,byte>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,int,UnityEngine.Color,int>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,int,int>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,int>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,object,byte>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,object,int>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,object>>
	// System.Collections.Generic.List.Enumerator<Unity.InferenceEngine.Model.Input>
	// System.Collections.Generic.List.Enumerator<UnityEngine.Color>
	// System.Collections.Generic.List.Enumerator<UnityEngine.CombineInstance>
	// System.Collections.Generic.List.Enumerator<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.Generic.List.Enumerator<UnityEngine.Quaternion>
	// System.Collections.Generic.List.Enumerator<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.List.Enumerator<UnityEngine.Vector2>
	// System.Collections.Generic.List.Enumerator<UnityEngine.Vector3>
	// System.Collections.Generic.List.Enumerator<UnityEngine.Vector4>
	// System.Collections.Generic.List.Enumerator<byte>
	// System.Collections.Generic.List.Enumerator<float>
	// System.Collections.Generic.List.Enumerator<int>
	// System.Collections.Generic.List.Enumerator<object>
	// System.Collections.Generic.List.Enumerator<uint>
	// System.Collections.Generic.List.Enumerator<ulong>
	// System.Collections.Generic.List<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.List<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.List<CardCore.MatchLogLine>
	// System.Collections.Generic.List<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.List<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.List<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.List<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.List<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.List<SynergyUI.BattleDeckPickStage.Entry>
	// System.Collections.Generic.List<SynergyUI.BattleScreen.CellActions>
	// System.Collections.Generic.List<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.List<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.List<System.ValueTuple<int,float>>
	// System.Collections.Generic.List<System.ValueTuple<int,int>>
	// System.Collections.Generic.List<System.ValueTuple<int,object,int>>
	// System.Collections.Generic.List<System.ValueTuple<int,object>>
	// System.Collections.Generic.List<System.ValueTuple<object,byte>>
	// System.Collections.Generic.List<System.ValueTuple<object,int,UnityEngine.Color,int>>
	// System.Collections.Generic.List<System.ValueTuple<object,int,int>>
	// System.Collections.Generic.List<System.ValueTuple<object,int>>
	// System.Collections.Generic.List<System.ValueTuple<object,object,byte>>
	// System.Collections.Generic.List<System.ValueTuple<object,object,int>>
	// System.Collections.Generic.List<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.List<System.ValueTuple<object,object>>
	// System.Collections.Generic.List<Unity.InferenceEngine.Model.Input>
	// System.Collections.Generic.List<UnityEngine.Color>
	// System.Collections.Generic.List<UnityEngine.CombineInstance>
	// System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.Generic.List<UnityEngine.Quaternion>
	// System.Collections.Generic.List<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.List<UnityEngine.Vector2>
	// System.Collections.Generic.List<UnityEngine.Vector3>
	// System.Collections.Generic.List<UnityEngine.Vector4>
	// System.Collections.Generic.List<byte>
	// System.Collections.Generic.List<float>
	// System.Collections.Generic.List<int>
	// System.Collections.Generic.List<object>
	// System.Collections.Generic.List<uint>
	// System.Collections.Generic.List<ulong>
	// System.Collections.Generic.ObjectComparer<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.ObjectComparer<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.ObjectComparer<CardCore.MatchLogLine>
	// System.Collections.Generic.ObjectComparer<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.Generic.ObjectComparer<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.ObjectComparer<MonsterRig.ScatterSim.Piece>
	// System.Collections.Generic.ObjectComparer<MonsterRig.WingBreathModule.Wing>
	// System.Collections.Generic.ObjectComparer<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.Generic.ObjectComparer<SynergyUI.BattleDeckPickStage.Entry>
	// System.Collections.Generic.ObjectComparer<SynergyUI.BattleScreen.CellActions>
	// System.Collections.Generic.ObjectComparer<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ObjectComparer<System.DateTime>
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
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,int>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<int,float>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<int,object,int>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<int,object>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,byte>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,int,UnityEngine.Color,int>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,int,int>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,object,byte>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,object,int>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,object,object>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.ObjectComparer<Unity.InferenceEngine.Model.Input>
	// System.Collections.Generic.ObjectComparer<UnityEngine.Color>
	// System.Collections.Generic.ObjectComparer<UnityEngine.CombineInstance>
	// System.Collections.Generic.ObjectComparer<UnityEngine.EventSystems.RaycastResult>
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
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,int>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<int,byte>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<int,int>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<int,object>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<object,int,int>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<object,object,byte>>
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
	// System.Collections.Generic.Queue.Enumerator<System.ValueTuple<object,byte>>
	// System.Collections.Generic.Queue.Enumerator<object>
	// System.Collections.Generic.Queue<System.ValueTuple<object,byte>>
	// System.Collections.Generic.Queue<object>
	// System.Collections.Generic.SortedSet.<>c__DisplayClass52_0<object>
	// System.Collections.Generic.SortedSet.<>c__DisplayClass52_0<ulong>
	// System.Collections.Generic.SortedSet.<>c__DisplayClass53_0<object>
	// System.Collections.Generic.SortedSet.<>c__DisplayClass53_0<ulong>
	// System.Collections.Generic.SortedSet.<>c__DisplayClass85_0<object>
	// System.Collections.Generic.SortedSet.<>c__DisplayClass85_0<ulong>
	// System.Collections.Generic.SortedSet.<Reverse>d__94<object>
	// System.Collections.Generic.SortedSet.<Reverse>d__94<ulong>
	// System.Collections.Generic.SortedSet.Enumerator<object>
	// System.Collections.Generic.SortedSet.Enumerator<ulong>
	// System.Collections.Generic.SortedSet.Node<object>
	// System.Collections.Generic.SortedSet.Node<ulong>
	// System.Collections.Generic.SortedSet.TreeSubSet.<>c__DisplayClass9_0<object>
	// System.Collections.Generic.SortedSet.TreeSubSet.<>c__DisplayClass9_0<ulong>
	// System.Collections.Generic.SortedSet.TreeSubSet<object>
	// System.Collections.Generic.SortedSet.TreeSubSet<ulong>
	// System.Collections.Generic.SortedSet<object>
	// System.Collections.Generic.SortedSet<ulong>
	// System.Collections.Generic.SortedSetEqualityComparer<object>
	// System.Collections.Generic.SortedSetEqualityComparer<ulong>
	// System.Collections.Generic.Stack.Enumerator<object>
	// System.Collections.Generic.Stack<object>
	// System.Collections.Generic.TreeWalkPredicate<object>
	// System.Collections.Generic.TreeWalkPredicate<ulong>
	// System.Collections.ObjectModel.ReadOnlyCollection<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.ObjectModel.ReadOnlyCollection<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.ObjectModel.ReadOnlyCollection<CardCore.MatchLogLine>
	// System.Collections.ObjectModel.ReadOnlyCollection<Cysharp.Threading.Tasks.UniTask>
	// System.Collections.ObjectModel.ReadOnlyCollection<MemoryPack.Internal.BufferSegment>
	// System.Collections.ObjectModel.ReadOnlyCollection<MonsterRig.ScatterSim.Piece>
	// System.Collections.ObjectModel.ReadOnlyCollection<MonsterRig.WingBreathModule.Wing>
	// System.Collections.ObjectModel.ReadOnlyCollection<SynergyUI.BattleCellPicker.CellOption>
	// System.Collections.ObjectModel.ReadOnlyCollection<SynergyUI.BattleDeckPickStage.Entry>
	// System.Collections.ObjectModel.ReadOnlyCollection<SynergyUI.BattleScreen.CellActions>
	// System.Collections.ObjectModel.ReadOnlyCollection<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<int,float,float>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<int,float>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<int,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<int,object,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<int,object>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,byte>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,int,UnityEngine.Color,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,int,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,object,byte>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,object,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,object,object>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,object>>
	// System.Collections.ObjectModel.ReadOnlyCollection<Unity.InferenceEngine.Model.Input>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Color>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.CombineInstance>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Quaternion>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Vector2>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Vector3>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Vector4>
	// System.Collections.ObjectModel.ReadOnlyCollection<byte>
	// System.Collections.ObjectModel.ReadOnlyCollection<float>
	// System.Collections.ObjectModel.ReadOnlyCollection<int>
	// System.Collections.ObjectModel.ReadOnlyCollection<object>
	// System.Collections.ObjectModel.ReadOnlyCollection<uint>
	// System.Collections.ObjectModel.ReadOnlyCollection<ulong>
	// System.Comparison<CardCore.AI.NeuralEnv.TideAction>
	// System.Comparison<CardCore.EventManager.SubscriptionEntry>
	// System.Comparison<CardCore.MatchLogLine>
	// System.Comparison<Cysharp.Threading.Tasks.UniTask>
	// System.Comparison<MemoryPack.Internal.BufferSegment>
	// System.Comparison<MonsterRig.ScatterSim.Piece>
	// System.Comparison<MonsterRig.WingBreathModule.Wing>
	// System.Comparison<SynergyUI.BattleCellPicker.CellOption>
	// System.Comparison<SynergyUI.BattleDeckPickStage.Entry>
	// System.Comparison<SynergyUI.BattleScreen.CellActions>
	// System.Comparison<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Comparison<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Comparison<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Comparison<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Comparison<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Comparison<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Comparison<System.DateTime>
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
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Comparison<System.ValueTuple<byte,byte>>
	// System.Comparison<System.ValueTuple<byte,int>>
	// System.Comparison<System.ValueTuple<byte,object>>
	// System.Comparison<System.ValueTuple<int,float,float>>
	// System.Comparison<System.ValueTuple<int,float>>
	// System.Comparison<System.ValueTuple<int,int>>
	// System.Comparison<System.ValueTuple<int,object,int>>
	// System.Comparison<System.ValueTuple<int,object,object>>
	// System.Comparison<System.ValueTuple<int,object>>
	// System.Comparison<System.ValueTuple<object,byte>>
	// System.Comparison<System.ValueTuple<object,int,UnityEngine.Color,int>>
	// System.Comparison<System.ValueTuple<object,int,int>>
	// System.Comparison<System.ValueTuple<object,int>>
	// System.Comparison<System.ValueTuple<object,object,byte>>
	// System.Comparison<System.ValueTuple<object,object,int>>
	// System.Comparison<System.ValueTuple<object,object,object>>
	// System.Comparison<System.ValueTuple<object,object>>
	// System.Comparison<Unity.InferenceEngine.Model.Input>
	// System.Comparison<UnityEngine.Color>
	// System.Comparison<UnityEngine.CombineInstance>
	// System.Comparison<UnityEngine.EventSystems.RaycastResult>
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
	// System.Func<SynergyUI.BattleDeckPickStage.Entry,byte>
	// System.Func<System.Collections.Generic.KeyValuePair<int,float>,System.ValueTuple<int,float>>
	// System.Func<System.Collections.Generic.KeyValuePair<int,float>,byte>
	// System.Func<System.Collections.Generic.KeyValuePair<int,float>,float>
	// System.Func<System.Collections.Generic.KeyValuePair<int,float>,int>
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
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Func<System.ValueTuple<byte,byte>>
	// System.Func<System.ValueTuple<byte,int>>
	// System.Func<System.ValueTuple<byte,object>>
	// System.Func<System.ValueTuple<int,float,float>,float>
	// System.Func<System.ValueTuple<int,float,float>,int>
	// System.Func<System.ValueTuple<int,float>,byte>
	// System.Func<System.ValueTuple<int,float>,int>
	// System.Func<System.ValueTuple<int,int>,byte>
	// System.Func<System.ValueTuple<int,int>,int>
	// System.Func<System.ValueTuple<int,object,object>,byte>
	// System.Func<System.ValueTuple<int,object,object>>
	// System.Func<System.ValueTuple<int,object>,byte>
	// System.Func<System.ValueTuple<int,object>,object>
	// System.Func<System.ValueTuple<object,int>,byte>
	// System.Func<System.ValueTuple<object,int>,object>
	// System.Func<System.ValueTuple<object,object,byte>,byte>
	// System.Func<System.ValueTuple<object,object,byte>,object>
	// System.Func<System.ValueTuple<object,object,byte>>
	// System.Func<System.ValueTuple<object,object,object>,System.ValueTuple<object,object,byte>>
	// System.Func<System.ValueTuple<object,object,object>,byte>
	// System.Func<System.ValueTuple<object,object>,System.ValueTuple<object,object>>
	// System.Func<System.ValueTuple<object,object>,byte>
	// System.Func<System.ValueTuple<object,object>,object>
	// System.Func<System.ValueTuple<object,object>>
	// System.Func<Unity.InferenceEngine.Model.Input,byte>
	// System.Func<Unity.InferenceEngine.Model.Input,object>
	// System.Func<UnityEngine.Vector4,byte>
	// System.Func<byte,byte>
	// System.Func<byte>
	// System.Func<double,byte>
	// System.Func<float,byte>
	// System.Func<float,double>
	// System.Func<float,float>
	// System.Func<int,Cysharp.Threading.Tasks.UniTask>
	// System.Func<int,UnityEngine.Vector4>
	// System.Func<int,byte>
	// System.Func<int,float>
	// System.Func<int,int,object>
	// System.Func<int,int>
	// System.Func<int,object>
	// System.Func<int>
	// System.Func<object,Cysharp.Threading.Tasks.UniTask>
	// System.Func<object,SynergyUI.BattleDeckPickStage.Entry>
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
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Func<object,System.ValueTuple<byte,byte>>
	// System.Func<object,System.ValueTuple<byte,int>>
	// System.Func<object,System.ValueTuple<byte,object>>
	// System.Func<object,System.ValueTuple<int,object,object>>
	// System.Func<object,System.ValueTuple<object,object,byte>>
	// System.Func<object,System.ValueTuple<object,object>>
	// System.Func<object,UnityEngine.Vector3>
	// System.Func<object,byte>
	// System.Func<object,float>
	// System.Func<object,int,byte>
	// System.Func<object,int,object>
	// System.Func<object,int>
	// System.Func<object,long>
	// System.Func<object,object,Cysharp.Threading.Tasks.UniTask<object>>
	// System.Func<object,object,byte>
	// System.Func<object,object,int,object>
	// System.Func<object,object,object,Cysharp.Threading.Tasks.UniTask<System.Nullable<byte>>>
	// System.Func<object,object,object,Cysharp.Threading.Tasks.UniTask<int>>
	// System.Func<object,object,object,object>
	// System.Func<object,object,object>
	// System.Func<object,object>
	// System.Func<object,uint>
	// System.Func<object>
	// System.Func<uint,byte>
	// System.Func<ushort,byte>
	// System.IComparable<CardCore.TimestampInfo>
	// System.IEquatable<CardCore.ColorIdentity>
	// System.IEquatable<CardCore.TimestampInfo>
	// System.Linq.Buffer<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.Buffer<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Linq.Buffer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.Buffer<System.ValueTuple<int,float,float>>
	// System.Linq.Buffer<System.ValueTuple<int,float>>
	// System.Linq.Buffer<System.ValueTuple<int,int>>
	// System.Linq.Buffer<System.ValueTuple<object,object>>
	// System.Linq.Buffer<UnityEngine.Vector4>
	// System.Linq.Buffer<int>
	// System.Linq.Buffer<object>
	// System.Linq.Buffer<uint>
	// System.Linq.Enumerable.<CastIterator>d__99<int>
	// System.Linq.Enumerable.<CastIterator>d__99<object>
	// System.Linq.Enumerable.<ConcatIterator>d__59<object>
	// System.Linq.Enumerable.<DistinctIterator>d__68<int>
	// System.Linq.Enumerable.<DistinctIterator>d__68<object>
	// System.Linq.Enumerable.<ExceptIterator>d__77<int>
	// System.Linq.Enumerable.<OfTypeIterator>d__97<object>
	// System.Linq.Enumerable.<ReverseIterator>d__79<object>
	// System.Linq.Enumerable.<SelectIterator>d__5<object,object>
	// System.Linq.Enumerable.<SelectManyIterator>d__17<object,object>
	// System.Linq.Enumerable.<SkipIterator>d__31<int>
	// System.Linq.Enumerable.<SkipIterator>d__31<object>
	// System.Linq.Enumerable.<TakeIterator>d__25<int>
	// System.Linq.Enumerable.<TakeIterator>d__25<object>
	// System.Linq.Enumerable.<TakeLastIterator>d__218<object>
	// System.Linq.Enumerable.<WhereIterator>d__2<object>
	// System.Linq.Enumerable.Iterator<SynergyUI.BattleDeckPickStage.Entry>
	// System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.Enumerable.Iterator<System.ValueTuple<int,float>>
	// System.Linq.Enumerable.Iterator<System.ValueTuple<int,int>>
	// System.Linq.Enumerable.Iterator<System.ValueTuple<int,object>>
	// System.Linq.Enumerable.Iterator<System.ValueTuple<object,int>>
	// System.Linq.Enumerable.Iterator<System.ValueTuple<object,object,byte>>
	// System.Linq.Enumerable.Iterator<System.ValueTuple<object,object,object>>
	// System.Linq.Enumerable.Iterator<System.ValueTuple<object,object>>
	// System.Linq.Enumerable.Iterator<Unity.InferenceEngine.Model.Input>
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
	// System.Linq.Enumerable.WhereArrayIterator<System.ValueTuple<int,int>>
	// System.Linq.Enumerable.WhereArrayIterator<System.ValueTuple<int,object>>
	// System.Linq.Enumerable.WhereArrayIterator<System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereArrayIterator<int>
	// System.Linq.Enumerable.WhereArrayIterator<object>
	// System.Linq.Enumerable.WhereArrayIterator<uint>
	// System.Linq.Enumerable.WhereEnumerableIterator<SynergyUI.BattleDeckPickStage.Entry>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.ValueTuple<int,float>>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.ValueTuple<int,int>>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.ValueTuple<int,object>>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.ValueTuple<object,object,byte>>
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
	// System.Linq.Enumerable.WhereListIterator<System.ValueTuple<int,int>>
	// System.Linq.Enumerable.WhereListIterator<System.ValueTuple<int,object>>
	// System.Linq.Enumerable.WhereListIterator<System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereListIterator<int>
	// System.Linq.Enumerable.WhereListIterator<object>
	// System.Linq.Enumerable.WhereListIterator<uint>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<int,float>,System.ValueTuple<int,float>>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<int,float>,int>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<int,float>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<int,int>,int>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<int,int>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.ValueTuple<int,int>,int>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.ValueTuple<int,object>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.ValueTuple<object,int>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.ValueTuple<object,object,byte>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.ValueTuple<object,object,object>,System.ValueTuple<object,object,byte>>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.ValueTuple<object,object>,System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.ValueTuple<object,object>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<Unity.InferenceEngine.Model.Input,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<float,double>
	// System.Linq.Enumerable.WhereSelectArrayIterator<float,float>
	// System.Linq.Enumerable.WhereSelectArrayIterator<int,UnityEngine.Vector4>
	// System.Linq.Enumerable.WhereSelectArrayIterator<int,int>
	// System.Linq.Enumerable.WhereSelectArrayIterator<int,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,SynergyUI.BattleDeckPickStage.Entry>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,byte>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,float>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,int>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,uint>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<int,float>,System.ValueTuple<int,float>>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<int,float>,int>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<int,float>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<int,int>,int>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<int,int>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.ValueTuple<int,int>,int>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.ValueTuple<int,object>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.ValueTuple<object,int>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.ValueTuple<object,object,byte>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.ValueTuple<object,object,object>,System.ValueTuple<object,object,byte>>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.ValueTuple<object,object>,System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.ValueTuple<object,object>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<Unity.InferenceEngine.Model.Input,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<float,double>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<float,float>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<int,UnityEngine.Vector4>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<int,int>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<int,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,SynergyUI.BattleDeckPickStage.Entry>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,byte>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,float>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,int>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,uint>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<int,float>,System.ValueTuple<int,float>>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<int,float>,int>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<int,float>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<int,int>,int>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<int,int>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.ValueTuple<int,int>,int>
	// System.Linq.Enumerable.WhereSelectListIterator<System.ValueTuple<int,object>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.ValueTuple<object,int>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.ValueTuple<object,object,byte>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.ValueTuple<object,object,object>,System.ValueTuple<object,object,byte>>
	// System.Linq.Enumerable.WhereSelectListIterator<System.ValueTuple<object,object>,System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereSelectListIterator<System.ValueTuple<object,object>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<Unity.InferenceEngine.Model.Input,object>
	// System.Linq.Enumerable.WhereSelectListIterator<float,double>
	// System.Linq.Enumerable.WhereSelectListIterator<float,float>
	// System.Linq.Enumerable.WhereSelectListIterator<int,UnityEngine.Vector4>
	// System.Linq.Enumerable.WhereSelectListIterator<int,int>
	// System.Linq.Enumerable.WhereSelectListIterator<int,object>
	// System.Linq.Enumerable.WhereSelectListIterator<object,SynergyUI.BattleDeckPickStage.Entry>
	// System.Linq.Enumerable.WhereSelectListIterator<object,System.ValueTuple<object,object>>
	// System.Linq.Enumerable.WhereSelectListIterator<object,byte>
	// System.Linq.Enumerable.WhereSelectListIterator<object,float>
	// System.Linq.Enumerable.WhereSelectListIterator<object,int>
	// System.Linq.Enumerable.WhereSelectListIterator<object,object>
	// System.Linq.Enumerable.WhereSelectListIterator<object,uint>
	// System.Linq.EnumerableSorter<System.Collections.Generic.KeyValuePair<int,float>,float>
	// System.Linq.EnumerableSorter<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.EnumerableSorter<System.Collections.Generic.KeyValuePair<int,int>,int>
	// System.Linq.EnumerableSorter<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Linq.EnumerableSorter<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.EnumerableSorter<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.EnumerableSorter<System.ValueTuple<int,float,float>,float>
	// System.Linq.EnumerableSorter<System.ValueTuple<int,float,float>>
	// System.Linq.EnumerableSorter<System.ValueTuple<int,float>,int>
	// System.Linq.EnumerableSorter<System.ValueTuple<int,float>>
	// System.Linq.EnumerableSorter<System.ValueTuple<int,int>,int>
	// System.Linq.EnumerableSorter<System.ValueTuple<int,int>>
	// System.Linq.EnumerableSorter<int,float>
	// System.Linq.EnumerableSorter<int,int>
	// System.Linq.EnumerableSorter<int>
	// System.Linq.EnumerableSorter<object,System.DateTime>
	// System.Linq.EnumerableSorter<object,System.Guid>
	// System.Linq.EnumerableSorter<object,byte>
	// System.Linq.EnumerableSorter<object,float>
	// System.Linq.EnumerableSorter<object,int>
	// System.Linq.EnumerableSorter<object,long>
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
	// System.Linq.IOrderedEnumerable<System.Collections.Generic.KeyValuePair<int,int>>
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
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<System.ValueTuple<int,float,float>>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<System.ValueTuple<int,float>>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<System.ValueTuple<int,int>>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<int>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<object>
	// System.Linq.OrderedEnumerable<System.Collections.Generic.KeyValuePair<int,float>,float>
	// System.Linq.OrderedEnumerable<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.OrderedEnumerable<System.Collections.Generic.KeyValuePair<int,int>,int>
	// System.Linq.OrderedEnumerable<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Linq.OrderedEnumerable<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.OrderedEnumerable<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.OrderedEnumerable<System.ValueTuple<int,float,float>,float>
	// System.Linq.OrderedEnumerable<System.ValueTuple<int,float,float>>
	// System.Linq.OrderedEnumerable<System.ValueTuple<int,float>,int>
	// System.Linq.OrderedEnumerable<System.ValueTuple<int,float>>
	// System.Linq.OrderedEnumerable<System.ValueTuple<int,int>,int>
	// System.Linq.OrderedEnumerable<System.ValueTuple<int,int>>
	// System.Linq.OrderedEnumerable<int,float>
	// System.Linq.OrderedEnumerable<int,int>
	// System.Linq.OrderedEnumerable<int>
	// System.Linq.OrderedEnumerable<object,System.DateTime>
	// System.Linq.OrderedEnumerable<object,System.Guid>
	// System.Linq.OrderedEnumerable<object,byte>
	// System.Linq.OrderedEnumerable<object,float>
	// System.Linq.OrderedEnumerable<object,int>
	// System.Linq.OrderedEnumerable<object,long>
	// System.Linq.OrderedEnumerable<object,object>
	// System.Linq.OrderedEnumerable<object,uint>
	// System.Linq.OrderedEnumerable<object>
	// System.Linq.Set<int>
	// System.Linq.Set<object>
	// System.Nullable<CardCore.Attribute.MorphSnapshot>
	// System.Nullable<CardCore.ComposerCatalog.EngineHeaderPreset>
	// System.Nullable<System.DateTime>
	// System.Nullable<System.ValueTuple<int,int>>
	// System.Nullable<System.ValueTuple<object,object>>
	// System.Nullable<System.ValueTuple<uint,int,int>>
	// System.Nullable<UnityEngine.Color>
	// System.Nullable<UnityEngine.Rect>
	// System.Nullable<UnityEngine.Vector2>
	// System.Nullable<UnityEngine.Vector3>
	// System.Nullable<byte>
	// System.Nullable<float>
	// System.Nullable<int>
	// System.Nullable<uint>
	// System.Predicate<CardCore.AI.NeuralEnv.TideAction>
	// System.Predicate<CardCore.EventManager.SubscriptionEntry>
	// System.Predicate<CardCore.MatchLogLine>
	// System.Predicate<Cysharp.Threading.Tasks.UniTask>
	// System.Predicate<MemoryPack.Internal.BufferSegment>
	// System.Predicate<MonsterRig.ScatterSim.Piece>
	// System.Predicate<MonsterRig.WingBreathModule.Wing>
	// System.Predicate<SynergyUI.BattleCellPicker.CellOption>
	// System.Predicate<SynergyUI.BattleDeckPickStage.Entry>
	// System.Predicate<SynergyUI.BattleScreen.CellActions>
	// System.Predicate<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Predicate<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Predicate<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Predicate<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Predicate<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Predicate<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Predicate<System.ValueTuple<int,float,float>>
	// System.Predicate<System.ValueTuple<int,float>>
	// System.Predicate<System.ValueTuple<int,int>>
	// System.Predicate<System.ValueTuple<int,object,int>>
	// System.Predicate<System.ValueTuple<int,object>>
	// System.Predicate<System.ValueTuple<object,byte>>
	// System.Predicate<System.ValueTuple<object,int,UnityEngine.Color,int>>
	// System.Predicate<System.ValueTuple<object,int,int>>
	// System.Predicate<System.ValueTuple<object,int>>
	// System.Predicate<System.ValueTuple<object,object,byte>>
	// System.Predicate<System.ValueTuple<object,object,int>>
	// System.Predicate<System.ValueTuple<object,object,object>>
	// System.Predicate<System.ValueTuple<object,object>>
	// System.Predicate<Unity.InferenceEngine.Model.Input>
	// System.Predicate<UnityEngine.Color>
	// System.Predicate<UnityEngine.CombineInstance>
	// System.Predicate<UnityEngine.EventSystems.RaycastResult>
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
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<object,object,byte>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<object,object>>
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
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<object,object,byte>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<object,object>>
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
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<object,object,byte>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<object,object>>
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
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<object,object,byte>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<object,object>>
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
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<object,object,byte>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<object,object>>
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
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<object,object,byte>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<object,object>>
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
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<object,object,byte>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<object,object>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<byte>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<int>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<object>
	// System.Span.Enumerator<byte>
	// System.Span.Enumerator<float>
	// System.Span.Enumerator<int>
	// System.Span.Enumerator<object>
	// System.Span.Enumerator<uint>
	// System.Span.Enumerator<ushort>
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
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<object,object,byte>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<object,object>>
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
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<object,object,byte>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<object,object>>
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
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.Task<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.Task<System.ValueTuple<object,object,byte>>
	// System.Threading.Tasks.Task<System.ValueTuple<object,object>>
	// System.Threading.Tasks.Task<byte>
	// System.Threading.Tasks.Task<int>
	// System.Threading.Tasks.Task<object>
	// System.Threading.Tasks.TaskCompletionSource<System.Nullable<byte>>
	// System.Threading.Tasks.TaskCompletionSource<int>
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
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<object,object,byte>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<object,object>>
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
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<object,object,byte>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<object,object>>
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
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<object,object,byte>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<object,object>>
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
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<object,object,byte>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<object,object>>
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
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<object,object,byte>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<object,object>>
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
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object,byte>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<object,object>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,byte>>
	// System.ValueTuple<byte,System.ValueTuple<byte,int>>
	// System.ValueTuple<byte,System.ValueTuple<byte,object>>
	// System.ValueTuple<byte,System.ValueTuple<int,object,object>>
	// System.ValueTuple<byte,System.ValueTuple<object,object,byte>>
	// System.ValueTuple<byte,System.ValueTuple<object,object>>
	// System.ValueTuple<byte,byte>
	// System.ValueTuple<byte,int>
	// System.ValueTuple<byte,object>
	// System.ValueTuple<int,byte>
	// System.ValueTuple<int,float,float>
	// System.ValueTuple<int,float>
	// System.ValueTuple<int,int,int>
	// System.ValueTuple<int,int>
	// System.ValueTuple<int,object,int>
	// System.ValueTuple<int,object,object,object,object>
	// System.ValueTuple<int,object,object>
	// System.ValueTuple<int,object>
	// System.ValueTuple<object,byte>
	// System.ValueTuple<object,int,UnityEngine.Color,int>
	// System.ValueTuple<object,int,int>
	// System.ValueTuple<object,int,object,object>
	// System.ValueTuple<object,int>
	// System.ValueTuple<object,object,byte>
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
	// UnityEngine.Events.InvokableCall<UnityEngine.Vector2>
	// UnityEngine.Events.InvokableCall<byte>
	// UnityEngine.Events.InvokableCall<float>
	// UnityEngine.Events.InvokableCall<int>
	// UnityEngine.Events.InvokableCall<object>
	// UnityEngine.Events.UnityAction<UnityEngine.Vector2>
	// UnityEngine.Events.UnityAction<byte>
	// UnityEngine.Events.UnityAction<float>
	// UnityEngine.Events.UnityAction<int>
	// UnityEngine.Events.UnityAction<object>
	// UnityEngine.Events.UnityEvent<UnityEngine.Vector2>
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
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ApplyActiveEngineTrunkAsync>d__25>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ApplyActiveEngineTrunkAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ApplyActiveLuckTrunkAsync>d__24>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ApplyActiveLuckTrunkAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__21>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ApplyOutcomeRewardsAsync>d__22>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ApplyOutcomeRewardsAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ApplySituationGateAsync>d__23>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ApplySituationGateAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ApplyThenToSharedTargetsAsync>d__26>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ApplyThenToSharedTargetsAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ExecuteAsync>d__16>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ExecuteAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ExecuteBranchRewardsAsync>d__30>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ExecuteBranchRewardsAsync>d__30&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ExecuteFlatAsync>d__17>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ExecuteFlatAsync>d__17&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ExecuteReferencedEffectAsync>d__31>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ExecuteReferencedEffectAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__19>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__19&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ExecuteStepsAsync>d__18>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ExecuteStepsAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameActions.<EndTurnWithPhaseWindowAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameActions.<EndTurnWithPhaseWindowAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameActions.<ForgetSafeAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameActions.<ForgetSafeAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameActions.<ResolveCardCastAsync>d__31>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameActions.<ResolveCardCastAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameActions.<ResolveSpellEffectsAsync>d__42>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameActions.<ResolveSpellEffectsAsync>d__42&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameActions.<SettleResponseWindowAsync>d__52>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameActions.<SettleResponseWindowAsync>d__52&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameLoopController.<ProcessPriority>d__12>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameLoopController.<ProcessPriority>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameLoopController.<ResolveStack>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameLoopController.<ResolveStack>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.StackEngine.<BeginResolution>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.StackEngine.<BeginResolution>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.StackEngine.<FinishResolution>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.StackEngine.<FinishResolution>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.StackEngine.<PlayerPass>d__0>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.StackEngine.<PlayerPass>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.StackEngine.<ResolveStack>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.StackEngine.<ResolveStack>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.StackEngine.<ResolveTop>d__11>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.StackEngine.<ResolveTop>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleCellPicker.<ConfirmAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleCellPicker.<ConfirmAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleController.<RunAiTurnAsync>d__29>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleController.<RunAiTurnAsync>d__29&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<>c__DisplayClass103_0.<<PresentDeathByRefAsync>b__0>d>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<>c__DisplayClass103_0.<<PresentDeathByRefAsync>b__0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<>c__DisplayClass43_0.<<PresentEffectDeathAsync>b__0>d>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<>c__DisplayClass43_0.<<PresentEffectDeathAsync>b__0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<ClashFxCoreAsync>d__39>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<ClashFxCoreAsync>d__39&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<DamageFxCoreAsync>d__29>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<DamageFxCoreAsync>d__29&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<DamageRefCoreAsync>d__97>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<DamageRefCoreAsync>d__97&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<FlightFxCoreAsync>d__45>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<FlightFxCoreAsync>d__45&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<FlightRefCoreAsync>d__101>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<FlightRefCoreAsync>d__101&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<HealFxCoreAsync>d__31>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<HealFxCoreAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<HealRefCoreAsync>d__99>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<HealRefCoreAsync>d__99&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PlayClashByRefAsync>d__108>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PlayClashByRefAsync>d__108&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PlayClashResolvedAsync>d__37>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PlayClashResolvedAsync>d__37&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PlayCombatShotAsync>d__111>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PlayCombatShotAsync>d__111&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PlayCounterFloatAsync>d__40>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PlayCounterFloatAsync>d__40&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PlayCounterFloatByRefAsync>d__104>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PlayCounterFloatByRefAsync>d__104&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PlayDamageByRefAsync>d__96>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PlayDamageByRefAsync>d__96&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PlayEffectDamageAsync>d__28>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PlayEffectDamageAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PlayEffectFlightAsync>d__44>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PlayEffectFlightAsync>d__44&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PlayEffectHealAsync>d__30>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PlayEffectHealAsync>d__30&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PlayFlightByRefAsync>d__100>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PlayFlightByRefAsync>d__100&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PlayGrantBurstAsync>d__35>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PlayGrantBurstAsync>d__35&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PlayGrantBurstByRefAsync>d__105>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PlayGrantBurstByRefAsync>d__105&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PlayHealByRefAsync>d__98>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PlayHealByRefAsync>d__98&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PlayLuckRollAsync>d__38>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PlayLuckRollAsync>d__38&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PlayLuckRollByRefAsync>d__109>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PlayLuckRollByRefAsync>d__109&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PresentDeathByRefAsync>d__103>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PresentDeathByRefAsync>d__103&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<PresentEffectDeathAsync>d__43>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<PresentEffectDeathAsync>d__43&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<RunUnitFxAsync>d__67>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<RunUnitFxAsync>d__67&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<WaitCastPayArrived>d__9>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<WaitCastPayArrived>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleOpeningDirector.<PlaceHeroesAsync>d__30>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleOpeningDirector.<PlaceHeroesAsync>d__30&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattlePlayhead.<RunReplayFrameAsync>d__39>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattlePlayhead.<RunReplayFrameAsync>d__39&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattlePlayhead.<WaitIdleAsync>d__43>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattlePlayhead.<WaitIdleAsync>d__43&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleScreen.<<MakePacedAi>b__122_0>d>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleScreen.<<MakePacedAi>b__122_0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleTutorialDirector.<<MakePacedScriptedAi>b__57_0>d>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleTutorialDirector.<<MakePacedScriptedAi>b__57_0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.CombatChoreographer.<ConfirmTargetAsync>d__46>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.CombatChoreographer.<ConfirmTargetAsync>d__46&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.Online.TapLobbyController.<LeaveAndDisconnectAsync>d__29>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.Online.TapLobbyController.<LeaveAndDisconnectAsync>d__29&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.ScriptedAi.<TakeTurnAsync>d__11>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.ScriptedAi.<TakeTurnAsync>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<DoCombatAsync>d__41>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<DoCombatAsync>d__41&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<PaceAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<PaceAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<StageCastAsync>d__16>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<StageCastAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<StageHoldAsync>d__15>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<StageHoldAsync>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<TakeTurnAsync>d__22>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<TakeTurnAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.Attribute.Handlers.DeclareDeckHandler.<ExecuteAsync>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.Attribute.Handlers.DeclareDeckHandler.<ExecuteAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.Attribute.Handlers.SearchDeckHandler.<ExecuteAsync>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.Attribute.Handlers.SearchDeckHandler.<ExecuteAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.CurseSystem.<>c__DisplayClass13_0.<<ExecutePayloadAsync>g__ExecuteAtomAsync|0>d>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.CurseSystem.<>c__DisplayClass13_0.<<ExecutePayloadAsync>g__ExecuteAtomAsync|0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.DelayedEffectScheduler.<Fire>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.DelayedEffectScheduler.<Fire>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.EffectExecutor.<>c__DisplayClass31_0.<<ExecuteReferencedEffectAsync>g__ExecuteAtomAsync|0>d>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.EffectExecutor.<>c__DisplayClass31_0.<<ExecuteReferencedEffectAsync>g__ExecuteAtomAsync|0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.EffectExecutor.<ApplyThenToSharedTargetsAsync>d__26>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.EffectExecutor.<ApplyThenToSharedTargetsAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.EffectExecutor.<ExecuteAsync>d__16>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.EffectExecutor.<ExecuteAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.EffectExecutor.<ExecuteFlatAsync>d__17>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.EffectExecutor.<ExecuteFlatAsync>d__17&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__19>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__19&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__28>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.EffectExecutor.<PayReplayRoundCostsAsync>d__39>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.EffectExecutor.<PayReplayRoundCostsAsync>d__39&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.GameActions.<ResolveCardCastAsync>d__31>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.GameActions.<ResolveCardCastAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.ProphecySystem.<ResolveAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.ProphecySystem.<ResolveAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CloudSave.CloudSyncService.<StartupSyncAsync>d__26>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CloudSave.CloudSyncService.<StartupSyncAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.BattleFieldView.<DamageFxCoreAsync>d__29>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.BattleFieldView.<DamageFxCoreAsync>d__29&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.BattleFieldView.<DamageRefCoreAsync>d__97>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.BattleFieldView.<DamageRefCoreAsync>d__97&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.BattleFieldView.<FlightFxCoreAsync>d__45>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.BattleFieldView.<FlightFxCoreAsync>d__45&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.BattleFieldView.<FlightRefCoreAsync>d__101>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.BattleFieldView.<FlightRefCoreAsync>d__101&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.BattleFieldView.<PlayDeathExitAsync>d__17>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.BattleFieldView.<PlayDeathExitAsync>d__17&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.CombatChoreographer.<ReturnAsync>d__78>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.CombatChoreographer.<ReturnAsync>d__78&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.ScriptedAi.<TakeTurnAsync>d__11>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.ScriptedAi.<TakeTurnAsync>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.SimpleAI.<TakeTurnAsync>d__22>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.SimpleAI.<TakeTurnAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<int>,CardCore.Attribute.Handlers.MoveUnitHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<int>&,CardCore.Attribute.Handlers.MoveUnitHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<int>,CardCore.GameActions.<ResolveChargeDeclarationAsync>d__63>(Cysharp.Threading.Tasks.UniTask.Awaiter<int>&,CardCore.GameActions.<ResolveChargeDeclarationAsync>d__63&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.DeclareDeckTopHandler.<ExecuteAsync>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.DeclareDeckTopHandler.<ExecuteAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.DiscardCardHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.DiscardCardHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.ProphecyNextCardHandler.<ExecuteAsync>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.ProphecyNextCardHandler.<ExecuteAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.RevealByDimensionHandler.<ExecuteAsync>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.RevealByDimensionHandler.<ExecuteAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.EffectExecutor.<ExecuteAsync>d__16>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.EffectExecutor.<ExecuteAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.EffectExecutor.<ExecuteFlatAsync>d__17>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.EffectExecutor.<ExecuteFlatAsync>d__17&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__19>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__19&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__28>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.GameActions.<SettleResponseWindowAsync>d__52>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.GameActions.<SettleResponseWindowAsync>d__52&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.GameCore.<EnforceHandLimitAsync>d__65>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.GameCore.<EnforceHandLimitAsync>d__65&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CloudSave.CloudSyncService.<StartupSyncAsync>d__26>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CloudSave.CloudSyncService.<StartupSyncAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleOpeningDirector.<NetworkDeckPickAsync>d__33>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleOpeningDirector.<NetworkDeckPickAsync>d__33&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.RankedMatchmaking.<HostWaitingRoom>d__36>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.RankedMatchmaking.<HostWaitingRoom>d__36&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.RankedMatchmaking.<JoinCandidate>d__35>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.RankedMatchmaking.<JoinCandidate>d__35&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,CloudSave.CloudTasks.<AwaitOnMain>d__1>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,CloudSave.CloudTasks.<AwaitOnMain>d__1&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleAttackArrow.<FadeOutAsync>d__23>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleAttackArrow.<FadeOutAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleBoardAssemble.<PlayAsync>d__9>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleBoardAssemble.<PlayAsync>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleDeckPickStage.<FlyToDeckAsync>d__32>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleDeckPickStage.<FlyToDeckAsync>d__32&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleEffectFx.<PlayOrbAsync>d__24>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleEffectFx.<PlayOrbAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleFieldView.<ArcMoveAsync>d__112>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleFieldView.<ArcMoveAsync>d__112&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleFieldView.<FloatTextAsync>d__47>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleFieldView.<FloatTextAsync>d__47&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleFieldView.<HeroFatigueHitAsync>d__22>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleFieldView.<HeroFatigueHitAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleFieldView.<HeroImpactPulseAsync>d__21>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleFieldView.<HeroImpactPulseAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleFieldView.<WaitVisualsIdleAsync>d__65>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleFieldView.<WaitVisualsIdleAsync>d__65&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleOpeningDirector.<BeatAsync>d__41>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleOpeningDirector.<BeatAsync>d__41&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleOpeningDirector.<PlaceHeroesAsync>d__30>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleOpeningDirector.<PlaceHeroesAsync>d__30&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleOpeningDirector.<WaitBoardAssembledAsync>d__29>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleOpeningDirector.<WaitBoardAssembledAsync>d__29&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleResolutionDirector.<WaitIdleAsync>d__30>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleResolutionDirector.<WaitIdleAsync>d__30&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleStageDirector.<TweenLightX>d__31>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleStageDirector.<TweenLightX>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleTutorialDirector.<<MakePacedScriptedAi>b__57_0>d>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleTutorialDirector.<<MakePacedScriptedAi>b__57_0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.CombatChoreographer.<DelayChainAware>d__81>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.CombatChoreographer.<DelayChainAware>d__81&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.CombatChoreographer.<WaitChainIdleAsync>d__82>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.CombatChoreographer.<WaitChainIdleAsync>d__82&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,CardCore.TideDelay.<After>d__0>(System.Runtime.CompilerServices.TaskAwaiter&,CardCore.TideDelay.<After>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.Nullable<byte>>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter<System.Nullable<byte>>,SynergyUI.BattleScreen.<ShowTipPromptAsync>d__267>(System.Runtime.CompilerServices.TaskAwaiter<System.Nullable<byte>>&,SynergyUI.BattleScreen.<ShowTipPromptAsync>d__267&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.Nullable<byte>>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter<System.Nullable<byte>>,SynergyUI.TipPromptHost.<ShowAsync>d__5>(System.Runtime.CompilerServices.TaskAwaiter<System.Nullable<byte>>&,SynergyUI.TipPromptHost.<ShowAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.Nullable<int>>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleScreen.<PickRegionOptionAsync>d__107>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleScreen.<PickRegionOptionAsync>d__107&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.ValueTuple<object,object,byte>>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<int>,CardCore.Attribute.Handlers.ProphecyHandlerUtil.<PickOptionAsync>d__6>(Cysharp.Threading.Tasks.UniTask.Awaiter<int>&,CardCore.Attribute.Handlers.ProphecyHandlerUtil.<PickOptionAsync>d__6&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.ValueTuple<object,object>>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleDeckPickStage.<PickAsync>d__21>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleDeckPickStage.<PickAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.ValueTuple<object,object>>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<object,object>>,SynergyUI.BattleOpeningDirector.<DeckPickStageAsync>d__34>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<object,object>>&,SynergyUI.BattleOpeningDirector.<DeckPickStageAsync>d__34&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.ValueTuple<object,object>>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleOpeningDirector.<DeckPickStageAsync>d__34>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleOpeningDirector.<DeckPickStageAsync>d__34&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.ValueTuple<object,object>>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleDeckPickStage.<PickAsync>d__21>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleDeckPickStage.<PickAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.CombatChoreographer.<FlyAsync>d__85>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.CombatChoreographer.<FlyAsync>d__85&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.ScriptedAi.<ExecuteAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.ScriptedAi.<ExecuteAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<PlayBestAffordableCardAsync>d__28>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<PlayBestAffordableCardAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<UseHeroSkillAsync>d__25>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<UseHeroSkillAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.CostCompensationService.<ExecutePayloadAtomAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.CostCompensationService.<ExecutePayloadAtomAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.CostCompensationService.<PayPayloadsAsync>d__0>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.CostCompensationService.<PayPayloadsAsync>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.GameActions.<ActivateHeroSkill>d__70>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.GameActions.<ActivateHeroSkill>d__70&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.BattleFieldView.<TryRouteCastChainAsync>d__95>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.BattleFieldView.<TryRouteCastChainAsync>d__95&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.BattleOpeningDirector.<CoinStageAsync>d__31>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.BattleOpeningDirector.<CoinStageAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.BattleOpeningDirector.<CoinStageForcedAsync>d__32>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.BattleOpeningDirector.<CoinStageForcedAsync>d__32&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.ScriptedAi.<ExecuteAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.ScriptedAi.<ExecuteAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.SimpleAI.<UseHeroSkillAsync>d__25>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.SimpleAI.<UseHeroSkillAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.ProphecyHandlerUtil.<AskCardNameAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.ProphecyHandlerUtil.<AskCardNameAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.CostCompensationService.<ExecutePayloadAtomAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.CostCompensationService.<ExecutePayloadAtomAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CloudSave.CloudSyncService.<RestoreFromCloudAsync>d__28>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CloudSave.CloudSyncService.<RestoreFromCloudAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CloudSave.CloudSyncService.<UploadAsync>d__27>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CloudSave.CloudSyncService.<UploadAsync>d__27&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CloudSave.TapAccountService.<EnsureLoginAsync>d__9>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CloudSave.TapAccountService.<EnsureLoginAsync>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleScreen.<NetPickUnitCellAsync>d__157>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleScreen.<NetPickUnitCellAsync>d__157&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleCoinToss.<PlayAsync>d__14>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleCoinToss.<PlayAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleFieldView.<DeadHoldAsync>d__18>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleFieldView.<DeadHoldAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleFieldView.<SinkUnderBoardAsync>d__19>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleFieldView.<SinkUnderBoardAsync>d__19&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleVfxFx.<TryPlayProjectileAsync>d__28>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleVfxFx.<TryPlayProjectileAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.CombatChoreographer.<FlyAsync>d__85>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.CombatChoreographer.<FlyAsync>d__85&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>,CardCore.TargetSelectionService.<RequestOneIndexAsync>d__23>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>&,CardCore.TargetSelectionService.<RequestOneIndexAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<int>,CardCore.TargetSelectionService.<RequestOneIndexAsync>d__23>(Cysharp.Threading.Tasks.UniTask.Awaiter<int>&,CardCore.TargetSelectionService.<RequestOneIndexAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<int>,SynergyUI.BattleScreen.<PickMoveDestinationAsync>d__98>(Cysharp.Threading.Tasks.UniTask.Awaiter<int>&,SynergyUI.BattleScreen.<PickMoveDestinationAsync>d__98&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__19>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__19&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.RegionTargetSelector.<SelectOneAsync>d__54>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.RegionTargetSelector.<SelectOneAsync>d__54&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.SwitchToMainThreadAwaitable.Awaiter,SynergyUI.Online.TapAccountService.<InitAndWaitAsync>d__21>(Cysharp.Threading.Tasks.SwitchToMainThreadAwaitable.Awaiter&,SynergyUI.Online.TapAccountService.<InitAndWaitAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.Network.NetworkTargetSelector.<SelectAsync>d__18>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.Network.NetworkTargetSelector.<SelectAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.Network.NetworkTargetSelector.<SelectTextAsync>d__20>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.Network.NetworkTargetSelector.<SelectTextAsync>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.Online.TapLobbySection.<CreateRoomCore>d__15>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.Online.TapLobbySection.<CreateRoomCore>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.Online.TapLobbySection.<LeaveAsync>d__17>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.Online.TapLobbySection.<LeaveAsync>d__17&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>,CardCore.Network.NetworkTargetSelector.<SelectAsync>d__18>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>&,CardCore.Network.NetworkTargetSelector.<SelectAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>,CardCore.Network.NetworkTargetSelector.<SelectTextAsync>d__20>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>&,CardCore.Network.NetworkTargetSelector.<SelectTextAsync>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>,CardCore.TargetSelectionService.<RequestAsync>d__22>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>&,CardCore.TargetSelectionService.<RequestAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>,SynergyUI.RegionTargetSelector.<PromptTextModalAsync>d__58>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>&,SynergyUI.RegionTargetSelector.<PromptTextModalAsync>d__58&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>,SynergyUI.RegionTargetSelector.<Run>d__59>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>&,SynergyUI.RegionTargetSelector.<Run>d__59&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>,SynergyUI.RegionTargetSelector.<RunChoice>d__53>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>&,SynergyUI.RegionTargetSelector.<RunChoice>d__53&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>,SynergyUI.RegionTargetSelector.<RunTextInputAsync>d__57>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>&,SynergyUI.RegionTargetSelector.<RunTextInputAsync>d__57&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<object,object,byte>>,CardCore.Attribute.Handlers.ProphecyHandlerUtil.<AskDimensionAsync>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<object,object,byte>>&,CardCore.Attribute.Handlers.ProphecyHandlerUtil.<AskDimensionAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<int>,CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter<int>&,CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.EffectHandlerRegistry.<AskCompositionDeclarationAsync>d__23>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.EffectHandlerRegistry.<AskCompositionDeclarationAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.EffectHandlerRegistry.<CollectDeclarationTargetsAsync>d__22>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.EffectHandlerRegistry.<CollectDeclarationTargetsAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.EffectHandlerRegistry.<ResolveAtomTargetsAsync>d__16>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.EffectHandlerRegistry.<ResolveAtomTargetsAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.EffectHandlerRegistry.<ResolveRewardTargetsAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.EffectHandlerRegistry.<ResolveRewardTargetsAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__15>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.ProphecyHandlerUtil.<AskDimensionAsync>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.ProphecyHandlerUtil.<AskDimensionAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.EffectExecutor.<ResolveSegmentedTargetsAsync>d__20>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.EffectExecutor.<ResolveSegmentedTargetsAsync>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.GameActions.<CollectSpellDeclarationTargetsAsync>d__41>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.GameActions.<CollectSpellDeclarationTargetsAsync>d__41&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.TargetSelectionService.<RequestAsync>d__22>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.TargetSelectionService.<RequestAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.TargetSelectionService.<RequestTextAsync>d__24>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.TargetSelectionService.<RequestTextAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleDecisionHub.<ShowResponseAsync>d__60>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleDecisionHub.<ShowResponseAsync>d__60&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleScreen.<PickCellAsync>d__164>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleScreen.<PickCellAsync>d__164&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.Online.TapAccountService.<EnsureReadyAsync>d__17>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.Online.TapAccountService.<EnsureReadyAsync>d__17&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.Online.TapAccountService.<LoginAsync>d__18>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.Online.TapAccountService.<LoginAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.Online.TapLobbyController.<CreateRoomAsync>d__26>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.Online.TapLobbyController.<CreateRoomAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.Online.TapLobbyController.<EnsureLoginAndConnectAsync>d__18>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.Online.TapLobbyController.<EnsureLoginAndConnectAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.Online.TapLobbyController.<GetRoomListAsync>d__27>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.Online.TapLobbyController.<GetRoomListAsync>d__27&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.Online.TapLobbyController.<JoinRoomAsync>d__28>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.Online.TapLobbyController.<JoinRoomAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.Online.TapLobbySection.<CreateRankedRoomAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.Online.TapLobbySection.<CreateRankedRoomAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.Online.TapLobbySection.<CreateRoomAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.Online.TapLobbySection.<CreateRoomAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.Online.TapLobbySection.<CreateRoomCore>d__15>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.Online.TapLobbySection.<CreateRoomCore>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.Online.TapLobbySection.<EnsureLoginAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.Online.TapLobbySection.<EnsureLoginAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.Online.TapLobbySection.<JoinRoomAsync>d__16>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.Online.TapLobbySection.<JoinRoomAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.Online.TapLobbySection.<ListRoomsAsync>d__11>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.Online.TapLobbySection.<ListRoomsAsync>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.RegionTargetSelector.<PromptTextAsync>d__56>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.RegionTargetSelector.<PromptTextAsync>d__56&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.RegionTargetSelector.<SelectAsync>d__48>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.RegionTargetSelector.<SelectAsync>d__48&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.RegionTargetSelector.<SelectIndicesAsync>d__50>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.RegionTargetSelector.<SelectIndicesAsync>d__50&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.RegionTargetSelector.<SelectTextAsync>d__55>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.RegionTargetSelector.<SelectTextAsync>d__55&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,CloudSave.CloudTasks.<AwaitOnMain>d__0<object>>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,CloudSave.CloudTasks.<AwaitOnMain>d__0<object>&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.DummyDeckPanel.<ShowAsync>d__13>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.DummyDeckPanel.<ShowAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.DummyOptionsPanel.<ShowAsync>d__9>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.DummyOptionsPanel.<ShowAsync>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.NetworkDeckPanel.<WaitAsync>d__15>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.NetworkDeckPanel.<WaitAsync>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.RegionTargetSelector.<Run>d__59>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.RegionTargetSelector.<Run>d__59&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,SynergyUI.Online.TapLobbyController.<EnsureLoginAndConnectAsync>d__18>(System.Runtime.CompilerServices.TaskAwaiter&,SynergyUI.Online.TapLobbyController.<EnsureLoginAndConnectAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter<object>,SynergyUI.Online.TapAccountService.<InitAndWaitAsync>d__21>(System.Runtime.CompilerServices.TaskAwaiter<object>&,SynergyUI.Online.TapAccountService.<InitAndWaitAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter<object>,SynergyUI.Online.TapAccountService.<LoginAsync>d__18>(System.Runtime.CompilerServices.TaskAwaiter<object>&,SynergyUI.Online.TapAccountService.<LoginAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter<object>,SynergyUI.Online.TapLobbyController.<EnsureLoginAndConnectAsync>d__18>(System.Runtime.CompilerServices.TaskAwaiter<object>&,SynergyUI.Online.TapLobbyController.<EnsureLoginAndConnectAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.DeclareDeckHandler.<ExecuteAsync>d__2>(CardCore.Attribute.Handlers.DeclareDeckHandler.<ExecuteAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.DeclareDeckTopHandler.<ExecuteAsync>d__2>(CardCore.Attribute.Handlers.DeclareDeckTopHandler.<ExecuteAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__2>(CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.DiscardCardHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.DiscardCardHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.MoveUnitHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.MoveUnitHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.ProphecyNextCardHandler.<ExecuteAsync>d__2>(CardCore.Attribute.Handlers.ProphecyNextCardHandler.<ExecuteAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.RevealByDimensionHandler.<ExecuteAsync>d__2>(CardCore.Attribute.Handlers.RevealByDimensionHandler.<ExecuteAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.SearchDeckHandler.<ExecuteAsync>d__2>(CardCore.Attribute.Handlers.SearchDeckHandler.<ExecuteAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.CurseSystem.<>c__DisplayClass13_0.<<ExecutePayloadAsync>g__ExecuteAtomAsync|0>d>(CardCore.CurseSystem.<>c__DisplayClass13_0.<<ExecutePayloadAsync>g__ExecuteAtomAsync|0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.CurseSystem.<ExecutePayloadAsync>d__13>(CardCore.CurseSystem.<ExecutePayloadAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.CurseSystem.<SettleAsync>d__12>(CardCore.CurseSystem.<SettleAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.DelayedEffectScheduler.<Fire>d__5>(CardCore.DelayedEffectScheduler.<Fire>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<>c__DisplayClass31_0.<<ExecuteReferencedEffectAsync>g__ExecuteAtomAsync|0>d>(CardCore.EffectExecutor.<>c__DisplayClass31_0.<<ExecuteReferencedEffectAsync>g__ExecuteAtomAsync|0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ApplyActiveEngineTrunkAsync>d__25>(CardCore.EffectExecutor.<ApplyActiveEngineTrunkAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ApplyActiveLuckTrunkAsync>d__24>(CardCore.EffectExecutor.<ApplyActiveLuckTrunkAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__21>(CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ApplyOutcomeRewardsAsync>d__22>(CardCore.EffectExecutor.<ApplyOutcomeRewardsAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ApplySituationGateAsync>d__23>(CardCore.EffectExecutor.<ApplySituationGateAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ApplyThenToSharedTargetsAsync>d__26>(CardCore.EffectExecutor.<ApplyThenToSharedTargetsAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteAsync>d__16>(CardCore.EffectExecutor.<ExecuteAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteBranchRewardsAsync>d__30>(CardCore.EffectExecutor.<ExecuteBranchRewardsAsync>d__30&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteFlatAsync>d__17>(CardCore.EffectExecutor.<ExecuteFlatAsync>d__17&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteReferencedEffectAsync>d__31>(CardCore.EffectExecutor.<ExecuteReferencedEffectAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__19>(CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__19&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteStepsAsync>d__18>(CardCore.EffectExecutor.<ExecuteStepsAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__28>(CardCore.EffectExecutor.<ExecuteThenRewardsAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<PayReplayRoundCostsAsync>d__39>(CardCore.EffectExecutor.<PayReplayRoundCostsAsync>d__39&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameActions.<EndTurnWithPhaseWindowAsync>d__4>(CardCore.GameActions.<EndTurnWithPhaseWindowAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameActions.<ForgetSafeAsync>d__10>(CardCore.GameActions.<ForgetSafeAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameActions.<ResolveCardCastAsync>d__31>(CardCore.GameActions.<ResolveCardCastAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameActions.<ResolveChargeDeclarationAsync>d__63>(CardCore.GameActions.<ResolveChargeDeclarationAsync>d__63&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameActions.<ResolveSpellEffectsAsync>d__42>(CardCore.GameActions.<ResolveSpellEffectsAsync>d__42&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameActions.<SettleResponseWindowAsync>d__52>(CardCore.GameActions.<SettleResponseWindowAsync>d__52&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameCore.<EnforceHandLimitAsync>d__65>(CardCore.GameCore.<EnforceHandLimitAsync>d__65&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameLoopController.<ProcessPriority>d__12>(CardCore.GameLoopController.<ProcessPriority>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameLoopController.<ResolveStack>d__13>(CardCore.GameLoopController.<ResolveStack>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.ProphecySystem.<ResolveAsync>d__10>(CardCore.ProphecySystem.<ResolveAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.StackEngine.<BeginResolution>d__3>(CardCore.StackEngine.<BeginResolution>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.StackEngine.<FinishResolution>d__5>(CardCore.StackEngine.<FinishResolution>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.StackEngine.<PlayerPass>d__0>(CardCore.StackEngine.<PlayerPass>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.StackEngine.<ResolveStack>d__4>(CardCore.StackEngine.<ResolveStack>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.StackEngine.<ResolveTop>d__11>(CardCore.StackEngine.<ResolveTop>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.TideDelay.<After>d__0>(CardCore.TideDelay.<After>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CloudSave.CloudSyncService.<StartupSyncAsync>d__26>(CloudSave.CloudSyncService.<StartupSyncAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CloudSave.CloudTasks.<AwaitOnMain>d__1>(CloudSave.CloudTasks.<AwaitOnMain>d__1&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleAttackArrow.<FadeOutAsync>d__23>(SynergyUI.BattleAttackArrow.<FadeOutAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleBoardAssemble.<PlayAsync>d__9>(SynergyUI.BattleBoardAssemble.<PlayAsync>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleCellPicker.<ConfirmAsync>d__14>(SynergyUI.BattleCellPicker.<ConfirmAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleController.<RunAiTurnAsync>d__29>(SynergyUI.BattleController.<RunAiTurnAsync>d__29&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleDeckPickStage.<FlyToDeckAsync>d__32>(SynergyUI.BattleDeckPickStage.<FlyToDeckAsync>d__32&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleEffectFx.<PlayOrbAsync>d__24>(SynergyUI.BattleEffectFx.<PlayOrbAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<>c__DisplayClass103_0.<<PresentDeathByRefAsync>b__0>d>(SynergyUI.BattleFieldView.<>c__DisplayClass103_0.<<PresentDeathByRefAsync>b__0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<>c__DisplayClass43_0.<<PresentEffectDeathAsync>b__0>d>(SynergyUI.BattleFieldView.<>c__DisplayClass43_0.<<PresentEffectDeathAsync>b__0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<ArcMoveAsync>d__112>(SynergyUI.BattleFieldView.<ArcMoveAsync>d__112&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<ClashFxCoreAsync>d__39>(SynergyUI.BattleFieldView.<ClashFxCoreAsync>d__39&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<DamageFxCoreAsync>d__29>(SynergyUI.BattleFieldView.<DamageFxCoreAsync>d__29&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<DamageRefCoreAsync>d__97>(SynergyUI.BattleFieldView.<DamageRefCoreAsync>d__97&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<FlightFxCoreAsync>d__45>(SynergyUI.BattleFieldView.<FlightFxCoreAsync>d__45&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<FlightRefCoreAsync>d__101>(SynergyUI.BattleFieldView.<FlightRefCoreAsync>d__101&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<FloatTextAsync>d__47>(SynergyUI.BattleFieldView.<FloatTextAsync>d__47&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<HealFxCoreAsync>d__31>(SynergyUI.BattleFieldView.<HealFxCoreAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<HealRefCoreAsync>d__99>(SynergyUI.BattleFieldView.<HealRefCoreAsync>d__99&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<HeroFatigueHitAsync>d__22>(SynergyUI.BattleFieldView.<HeroFatigueHitAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<HeroImpactPulseAsync>d__21>(SynergyUI.BattleFieldView.<HeroImpactPulseAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayClashByRefAsync>d__108>(SynergyUI.BattleFieldView.<PlayClashByRefAsync>d__108&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayClashResolvedAsync>d__37>(SynergyUI.BattleFieldView.<PlayClashResolvedAsync>d__37&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayCombatShotAsync>d__111>(SynergyUI.BattleFieldView.<PlayCombatShotAsync>d__111&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayCounterFloatAsync>d__40>(SynergyUI.BattleFieldView.<PlayCounterFloatAsync>d__40&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayCounterFloatByRefAsync>d__104>(SynergyUI.BattleFieldView.<PlayCounterFloatByRefAsync>d__104&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayDamageByRefAsync>d__96>(SynergyUI.BattleFieldView.<PlayDamageByRefAsync>d__96&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayDeathExitAsync>d__17>(SynergyUI.BattleFieldView.<PlayDeathExitAsync>d__17&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayEffectDamageAsync>d__28>(SynergyUI.BattleFieldView.<PlayEffectDamageAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayEffectFlightAsync>d__44>(SynergyUI.BattleFieldView.<PlayEffectFlightAsync>d__44&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayEffectHealAsync>d__30>(SynergyUI.BattleFieldView.<PlayEffectHealAsync>d__30&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayFlightByRefAsync>d__100>(SynergyUI.BattleFieldView.<PlayFlightByRefAsync>d__100&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayGrantBurstAsync>d__35>(SynergyUI.BattleFieldView.<PlayGrantBurstAsync>d__35&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayGrantBurstByRefAsync>d__105>(SynergyUI.BattleFieldView.<PlayGrantBurstByRefAsync>d__105&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayHealByRefAsync>d__98>(SynergyUI.BattleFieldView.<PlayHealByRefAsync>d__98&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayLuckRollAsync>d__38>(SynergyUI.BattleFieldView.<PlayLuckRollAsync>d__38&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PlayLuckRollByRefAsync>d__109>(SynergyUI.BattleFieldView.<PlayLuckRollByRefAsync>d__109&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PresentDeathByRefAsync>d__103>(SynergyUI.BattleFieldView.<PresentDeathByRefAsync>d__103&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<PresentEffectDeathAsync>d__43>(SynergyUI.BattleFieldView.<PresentEffectDeathAsync>d__43&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<RunUnitFxAsync>d__67>(SynergyUI.BattleFieldView.<RunUnitFxAsync>d__67&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<WaitCastPayArrived>d__9>(SynergyUI.BattleFieldView.<WaitCastPayArrived>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleFieldView.<WaitVisualsIdleAsync>d__65>(SynergyUI.BattleFieldView.<WaitVisualsIdleAsync>d__65&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleOpeningDirector.<BeatAsync>d__41>(SynergyUI.BattleOpeningDirector.<BeatAsync>d__41&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleOpeningDirector.<NetworkDeckPickAsync>d__33>(SynergyUI.BattleOpeningDirector.<NetworkDeckPickAsync>d__33&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleOpeningDirector.<PlaceHeroesAsync>d__30>(SynergyUI.BattleOpeningDirector.<PlaceHeroesAsync>d__30&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleOpeningDirector.<WaitBoardAssembledAsync>d__29>(SynergyUI.BattleOpeningDirector.<WaitBoardAssembledAsync>d__29&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattlePlayhead.<RunReplayFrameAsync>d__39>(SynergyUI.BattlePlayhead.<RunReplayFrameAsync>d__39&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattlePlayhead.<WaitIdleAsync>d__43>(SynergyUI.BattlePlayhead.<WaitIdleAsync>d__43&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleResolutionDirector.<WaitIdleAsync>d__30>(SynergyUI.BattleResolutionDirector.<WaitIdleAsync>d__30&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleScreen.<<MakePacedAi>b__122_0>d>(SynergyUI.BattleScreen.<<MakePacedAi>b__122_0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleStageDirector.<TweenLightX>d__31>(SynergyUI.BattleStageDirector.<TweenLightX>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleTutorialDirector.<<MakePacedScriptedAi>b__57_0>d>(SynergyUI.BattleTutorialDirector.<<MakePacedScriptedAi>b__57_0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.CombatChoreographer.<ConfirmTargetAsync>d__46>(SynergyUI.CombatChoreographer.<ConfirmTargetAsync>d__46&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.CombatChoreographer.<DelayChainAware>d__81>(SynergyUI.CombatChoreographer.<DelayChainAware>d__81&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.CombatChoreographer.<ReturnAsync>d__78>(SynergyUI.CombatChoreographer.<ReturnAsync>d__78&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.CombatChoreographer.<WaitChainIdleAsync>d__82>(SynergyUI.CombatChoreographer.<WaitChainIdleAsync>d__82&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6>(SynergyUI.NeuralAiDriver.<TakeTurnAsync>d__6&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.Online.TapLobbyController.<LeaveAndDisconnectAsync>d__29>(SynergyUI.Online.TapLobbyController.<LeaveAndDisconnectAsync>d__29&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.RankedMatchmaking.<HostWaitingRoom>d__36>(SynergyUI.RankedMatchmaking.<HostWaitingRoom>d__36&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.RankedMatchmaking.<JoinCandidate>d__35>(SynergyUI.RankedMatchmaking.<JoinCandidate>d__35&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.ScriptedAi.<TakeTurnAsync>d__11>(SynergyUI.ScriptedAi.<TakeTurnAsync>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.SimpleAI.<DoCombatAsync>d__41>(SynergyUI.SimpleAI.<DoCombatAsync>d__41&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.SimpleAI.<PaceAsync>d__14>(SynergyUI.SimpleAI.<PaceAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.SimpleAI.<StageCastAsync>d__16>(SynergyUI.SimpleAI.<StageCastAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.SimpleAI.<StageHoldAsync>d__15>(SynergyUI.SimpleAI.<StageHoldAsync>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.SimpleAI.<TakeTurnAsync>d__22>(SynergyUI.SimpleAI.<TakeTurnAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.Nullable<byte>>.Start<SynergyUI.BattleScreen.<ShowTipPromptAsync>d__267>(SynergyUI.BattleScreen.<ShowTipPromptAsync>d__267&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.Nullable<byte>>.Start<SynergyUI.TipPromptHost.<ShowAsync>d__5>(SynergyUI.TipPromptHost.<ShowAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.Nullable<int>>.Start<SynergyUI.BattleScreen.<PickRegionOptionAsync>d__107>(SynergyUI.BattleScreen.<PickRegionOptionAsync>d__107&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.ValueTuple<object,object,byte>>.Start<CardCore.Attribute.Handlers.ProphecyHandlerUtil.<PickOptionAsync>d__6>(CardCore.Attribute.Handlers.ProphecyHandlerUtil.<PickOptionAsync>d__6&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.ValueTuple<object,object>>.Start<SynergyUI.BattleDeckPickStage.<PickAsync>d__21>(SynergyUI.BattleDeckPickStage.<PickAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<System.ValueTuple<object,object>>.Start<SynergyUI.BattleOpeningDirector.<DeckPickStageAsync>d__34>(SynergyUI.BattleOpeningDirector.<DeckPickStageAsync>d__34&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5>(CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CardCore.Attribute.Handlers.ProphecyHandlerUtil.<AskCardNameAsync>d__4>(CardCore.Attribute.Handlers.ProphecyHandlerUtil.<AskCardNameAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CardCore.CostCompensationService.<ExecutePayloadAtomAsync>d__3>(CardCore.CostCompensationService.<ExecutePayloadAtomAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CardCore.CostCompensationService.<PayPayloadsAsync>d__0>(CardCore.CostCompensationService.<PayPayloadsAsync>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CardCore.GameActions.<ActivateHeroSkill>d__70>(CardCore.GameActions.<ActivateHeroSkill>d__70&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CardCore.HeroSkillSystem.<ActivateAsync>d__20>(CardCore.HeroSkillSystem.<ActivateAsync>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CloudSave.CloudSyncService.<RestoreFromCloudAsync>d__28>(CloudSave.CloudSyncService.<RestoreFromCloudAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CloudSave.CloudSyncService.<UploadAsync>d__27>(CloudSave.CloudSyncService.<UploadAsync>d__27&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CloudSave.TapAccountService.<EnsureLoginAsync>d__9>(CloudSave.TapAccountService.<EnsureLoginAsync>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<Cysharp.Threading.Tasks.UniTask.<RunOnThreadPool>d__86<byte>>(Cysharp.Threading.Tasks.UniTask.<RunOnThreadPool>d__86<byte>&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.BattleCoinToss.<PlayAsync>d__14>(SynergyUI.BattleCoinToss.<PlayAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.BattleFieldView.<DeadHoldAsync>d__18>(SynergyUI.BattleFieldView.<DeadHoldAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.BattleFieldView.<SinkUnderBoardAsync>d__19>(SynergyUI.BattleFieldView.<SinkUnderBoardAsync>d__19&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.BattleFieldView.<TryRouteCastChainAsync>d__95>(SynergyUI.BattleFieldView.<TryRouteCastChainAsync>d__95&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.BattleOpeningDirector.<CoinStageAsync>d__31>(SynergyUI.BattleOpeningDirector.<CoinStageAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.BattleOpeningDirector.<CoinStageForcedAsync>d__32>(SynergyUI.BattleOpeningDirector.<CoinStageForcedAsync>d__32&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.BattleScreen.<NetPickUnitCellAsync>d__157>(SynergyUI.BattleScreen.<NetPickUnitCellAsync>d__157&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.BattleVfxFx.<TryPlayProjectileAsync>d__28>(SynergyUI.BattleVfxFx.<TryPlayProjectileAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.CombatChoreographer.<FlyAsync>d__85>(SynergyUI.CombatChoreographer.<FlyAsync>d__85&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.ScriptedAi.<ExecuteAsync>d__13>(SynergyUI.ScriptedAi.<ExecuteAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.SimpleAI.<PlayBestAffordableCardAsync>d__28>(SynergyUI.SimpleAI.<PlayBestAffordableCardAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<SynergyUI.SimpleAI.<UseHeroSkillAsync>d__25>(SynergyUI.SimpleAI.<UseHeroSkillAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.Start<CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__19>(CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__19&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.Start<CardCore.TargetSelectionService.<RequestOneIndexAsync>d__23>(CardCore.TargetSelectionService.<RequestOneIndexAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.Start<SynergyUI.BattleScreen.<PickMoveDestinationAsync>d__98>(SynergyUI.BattleScreen.<PickMoveDestinationAsync>d__98&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.Start<SynergyUI.RegionTargetSelector.<SelectOneAsync>d__54>(SynergyUI.RegionTargetSelector.<SelectOneAsync>d__54&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.EffectHandlerRegistry.<AskCompositionDeclarationAsync>d__23>(CardCore.Attribute.EffectHandlerRegistry.<AskCompositionDeclarationAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.EffectHandlerRegistry.<CollectDeclarationTargetsAsync>d__22>(CardCore.Attribute.EffectHandlerRegistry.<CollectDeclarationTargetsAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.EffectHandlerRegistry.<ResolveAtomTargetsAsync>d__16>(CardCore.Attribute.EffectHandlerRegistry.<ResolveAtomTargetsAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__10>(CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.EffectHandlerRegistry.<ResolveRewardTargetsAsync>d__14>(CardCore.Attribute.EffectHandlerRegistry.<ResolveRewardTargetsAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__15>(CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2>(CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.Handlers.ProphecyHandlerUtil.<AskDimensionAsync>d__5>(CardCore.Attribute.Handlers.ProphecyHandlerUtil.<AskDimensionAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.EffectExecutor.<ResolveSegmentedTargetsAsync>d__20>(CardCore.EffectExecutor.<ResolveSegmentedTargetsAsync>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.GameActions.<CollectSpellDeclarationTargetsAsync>d__41>(CardCore.GameActions.<CollectSpellDeclarationTargetsAsync>d__41&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Network.NetworkTargetSelector.<SelectAsync>d__18>(CardCore.Network.NetworkTargetSelector.<SelectAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Network.NetworkTargetSelector.<SelectTextAsync>d__20>(CardCore.Network.NetworkTargetSelector.<SelectTextAsync>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.TargetSelectionService.<RequestAsync>d__22>(CardCore.TargetSelectionService.<RequestAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.TargetSelectionService.<RequestTextAsync>d__24>(CardCore.TargetSelectionService.<RequestTextAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CloudSave.CloudTasks.<AwaitOnMain>d__0<object>>(CloudSave.CloudTasks.<AwaitOnMain>d__0<object>&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<Cysharp.Threading.Tasks.UniTaskExtensions.<ContinueWith>d__60<object>>(Cysharp.Threading.Tasks.UniTaskExtensions.<ContinueWith>d__60<object>&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<Cysharp.Threading.Tasks.UniTaskExtensions.<Timeout>d__45<object>>(Cysharp.Threading.Tasks.UniTaskExtensions.<Timeout>d__45<object>&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.BattleDecisionHub.<ShowResponseAsync>d__60>(SynergyUI.BattleDecisionHub.<ShowResponseAsync>d__60&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.BattleScreen.<PickCellAsync>d__164>(SynergyUI.BattleScreen.<PickCellAsync>d__164&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.DummyDeckPanel.<ShowAsync>d__13>(SynergyUI.DummyDeckPanel.<ShowAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.DummyOptionsPanel.<ShowAsync>d__9>(SynergyUI.DummyOptionsPanel.<ShowAsync>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.NetworkDeckPanel.<WaitAsync>d__15>(SynergyUI.NetworkDeckPanel.<WaitAsync>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.Online.TapAccountService.<EnsureReadyAsync>d__17>(SynergyUI.Online.TapAccountService.<EnsureReadyAsync>d__17&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.Online.TapAccountService.<InitAndWaitAsync>d__21>(SynergyUI.Online.TapAccountService.<InitAndWaitAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.Online.TapAccountService.<LoginAsync>d__18>(SynergyUI.Online.TapAccountService.<LoginAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.Online.TapLobbyController.<CreateRoomAsync>d__26>(SynergyUI.Online.TapLobbyController.<CreateRoomAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.Online.TapLobbyController.<EnsureLoginAndConnectAsync>d__18>(SynergyUI.Online.TapLobbyController.<EnsureLoginAndConnectAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.Online.TapLobbyController.<GetRoomListAsync>d__27>(SynergyUI.Online.TapLobbyController.<GetRoomListAsync>d__27&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.Online.TapLobbyController.<JoinRoomAsync>d__28>(SynergyUI.Online.TapLobbyController.<JoinRoomAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.Online.TapLobbySection.<CreateRankedRoomAsync>d__14>(SynergyUI.Online.TapLobbySection.<CreateRankedRoomAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.Online.TapLobbySection.<CreateRoomAsync>d__13>(SynergyUI.Online.TapLobbySection.<CreateRoomAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.Online.TapLobbySection.<CreateRoomCore>d__15>(SynergyUI.Online.TapLobbySection.<CreateRoomCore>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.Online.TapLobbySection.<EnsureLoginAsync>d__10>(SynergyUI.Online.TapLobbySection.<EnsureLoginAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.Online.TapLobbySection.<JoinRoomAsync>d__16>(SynergyUI.Online.TapLobbySection.<JoinRoomAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.Online.TapLobbySection.<LeaveAsync>d__17>(SynergyUI.Online.TapLobbySection.<LeaveAsync>d__17&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.Online.TapLobbySection.<ListRoomsAsync>d__11>(SynergyUI.Online.TapLobbySection.<ListRoomsAsync>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.RegionTargetSelector.<PromptTextAsync>d__56>(SynergyUI.RegionTargetSelector.<PromptTextAsync>d__56&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.RegionTargetSelector.<PromptTextModalAsync>d__58>(SynergyUI.RegionTargetSelector.<PromptTextModalAsync>d__58&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.RegionTargetSelector.<Run>d__59>(SynergyUI.RegionTargetSelector.<Run>d__59&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.RegionTargetSelector.<RunChoice>d__53>(SynergyUI.RegionTargetSelector.<RunChoice>d__53&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.RegionTargetSelector.<RunTextInputAsync>d__57>(SynergyUI.RegionTargetSelector.<RunTextInputAsync>d__57&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.RegionTargetSelector.<SelectAsync>d__48>(SynergyUI.RegionTargetSelector.<SelectAsync>d__48&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.RegionTargetSelector.<SelectIndicesAsync>d__50>(SynergyUI.RegionTargetSelector.<SelectIndicesAsync>d__50&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.RegionTargetSelector.<SelectTextAsync>d__55>(SynergyUI.RegionTargetSelector.<SelectTextAsync>d__55&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CloudSave.TapAccountService.<LoginFromUiAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CloudSave.TapAccountService.<LoginFromUiAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CloudSave.TapCloudSaveBootstrap.BootInitCallback.<RunAsync>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CloudSave.TapCloudSaveBootstrap.BootInitCallback.<RunAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleAiCastStage.<FadeDisposeArrowAsync>d__15>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleAiCastStage.<FadeDisposeArrowAsync>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleCellPicker.<LoopAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleCellPicker.<LoopAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleDecisionHub.<NotChainCountdownAsync>d__62>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleDecisionHub.<NotChainCountdownAsync>d__62&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleEffectFx.<LingerOrbAsync>d__25>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleEffectFx.<LingerOrbAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleEffectFx.Runner.<LifeAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleEffectFx.Runner.<LifeAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleFieldView.<LingerCastingAsync>d__1>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleFieldView.<LingerCastingAsync>d__1&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleHudRow.<PlayPhaseRingAsync>d__48>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleHudRow.<PlayPhaseRingAsync>d__48&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleOpeningDirector.<RunAsync>d__28>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleOpeningDirector.<RunAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattlePlayhead.<FinishAsync>d__40>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattlePlayhead.<FinishAsync>d__40&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattlePlayhead.<RunPlaybackAsync>d__38>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattlePlayhead.<RunPlaybackAsync>d__38&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleResolutionDirector.<FinishAsync>d__27>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleResolutionDirector.<FinishAsync>d__27&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleResolutionDirector.<RunReplayAsync>d__26>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleResolutionDirector.<RunReplayAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleScreen.<PulseAutoTargetGlowAsync>d__273>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleScreen.<PulseAutoTargetGlowAsync>d__273&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleScreen.<RejectDeckAndBackAsync>d__180>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleScreen.<RejectDeckAndBackAsync>d__180&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleScreen.<RunAiFirstTurnAsync>d__118>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleScreen.<RunAiFirstTurnAsync>d__118&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleStageDirector.<MountBoardAndDawnAsync>d__25>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleStageDirector.<MountBoardAndDawnAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleTutorialDirector.<AutoEndTurnAfterHealFxAsync>d__41>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleTutorialDirector.<AutoEndTurnAfterHealFxAsync>d__41&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleTutorialDirector.<ConsumeMidTransitionAsync>d__43>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleTutorialDirector.<ConsumeMidTransitionAsync>d__43&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleTutorialDirector.<PromptTutorialRetryAsync>d__68>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleTutorialDirector.<PromptTutorialRetryAsync>d__68&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleVfxFx.<ReleaseSlotAfter>d__18>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleVfxFx.<ReleaseSlotAfter>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.CombatChoreographer.<PerformAsync>d__76>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.CombatChoreographer.<PerformAsync>d__76&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__45>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__45&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.LaunchScreen.<AutoAdvance>d__18>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.LaunchScreen.<AutoAdvance>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.LobbyScreen.<RunScanLoopAsync>d__123>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.LobbyScreen.<RunScanLoopAsync>d__123&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.RankedMatchmaking.<RunLoop>d__33>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.RankedMatchmaking.<RunLoop>d__33&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.RankedMatchmaking.<ScanOnce>d__34>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.RankedMatchmaking.<ScanOnce>d__34&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>,SynergyUI.BattleScreen.<OnConcede>d__106>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>&,SynergyUI.BattleScreen.<OnConcede>d__106&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>,SynergyUI.BattleScreen.<StartGauntletOpeningAsync>d__113>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>&,SynergyUI.BattleScreen.<StartGauntletOpeningAsync>d__113&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>,SynergyUI.BattleTutorialDirector.<PromptGauntletOverAsync>d__70>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>&,SynergyUI.BattleTutorialDirector.<PromptGauntletOverAsync>d__70&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>,SynergyUI.BattleTutorialDirector.<PromptTutorialCompleteAsync>d__69>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>&,SynergyUI.BattleTutorialDirector.<PromptTutorialCompleteAsync>d__69&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>,SynergyUI.BattleTutorialDirector.<PromptTutorialRetryAsync>d__68>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>&,SynergyUI.BattleTutorialDirector.<PromptTutorialRetryAsync>d__68&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>,SynergyUI.CardComposerScreen.<ConfirmForceDeleteCardAsync>d__141>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>&,SynergyUI.CardComposerScreen.<ConfirmForceDeleteCardAsync>d__141&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>,SynergyUI.DeckBuilderScreen.<ConfirmCopyPresetAsync>d__115>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>&,SynergyUI.DeckBuilderScreen.<ConfirmCopyPresetAsync>d__115&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>,SynergyUI.DeckBuilderScreen.<ConfirmDeleteDeckAsync>d__117>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>&,SynergyUI.DeckBuilderScreen.<ConfirmDeleteDeckAsync>d__117&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>,SynergyUI.EffectComposerScreen.<ConfirmForceDeleteEffectAsync>d__127>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>&,SynergyUI.EffectComposerScreen.<ConfirmForceDeleteEffectAsync>d__127&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>,SynergyUI.EffectComposerScreen.<HandleReplacedLegacyAsync>d__128>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>&,SynergyUI.EffectComposerScreen.<HandleReplacedLegacyAsync>d__128&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>,SynergyUI.TapLoginBadge.<LoginFlow>d__17>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<byte>>&,SynergyUI.TapLoginBadge.<LoginFlow>d__17&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>,SynergyUI.BattleScreen.<ActivateSkillAsync>d__105>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>&,SynergyUI.BattleScreen.<ActivateSkillAsync>d__105&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>,SynergyUI.BattleScreen.<NetActivateSkillAsync>d__168>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>&,SynergyUI.BattleScreen.<NetActivateSkillAsync>d__168&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>,SynergyUI.BattleScreen.<NetAttackFlowAsync>d__166>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>&,SynergyUI.BattleScreen.<NetAttackFlowAsync>d__166&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>,SynergyUI.BattleScreen.<NetGuardFlowAsync>d__167>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>&,SynergyUI.BattleScreen.<NetGuardFlowAsync>d__167&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>,SynergyUI.BattleScreen.<NetPlaceLandAsync>d__160>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>&,SynergyUI.BattleScreen.<NetPlaceLandAsync>d__160&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>,SynergyUI.BattleScreen.<NetSelectAsync>d__173>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>&,SynergyUI.BattleScreen.<NetSelectAsync>d__173&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>,SynergyUI.BattleScreen.<NetUseCardAsync>d__156>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>&,SynergyUI.BattleScreen.<NetUseCardAsync>d__156&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>,SynergyUI.BattleScreen.<OnClickUnitEffectButton>d__33>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>&,SynergyUI.BattleScreen.<OnClickUnitEffectButton>d__33&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>,SynergyUI.BattleScreen.<PickLandColorAsync>d__77>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>&,SynergyUI.BattleScreen.<PickLandColorAsync>d__77&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>,SynergyUI.BattleScreen.<PromptModeThenPickCell>d__96>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.Nullable<int>>&,SynergyUI.BattleScreen.<PromptModeThenPickCell>d__96&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<object,object>>,SynergyUI.BattleOpeningDirector.<RunAsync>d__28>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<object,object>>&,SynergyUI.BattleOpeningDirector.<RunAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CloudSave.CloudSyncService.CloudSyncHost.<TryAutoUpload>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CloudSave.CloudSyncService.CloudSyncHost.<TryAutoUpload>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CloudSave.TapAccountService.<LoginFromUiAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CloudSave.TapAccountService.<LoginFromUiAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CloudSave.TapCloudSaveBootstrap.BootInitCallback.<RunAsync>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CloudSave.TapCloudSaveBootstrap.BootInitCallback.<RunAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.BattleOpeningDirector.<RunAsync>d__28>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.BattleOpeningDirector.<RunAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.BattleScreen.<NetUseCardAsync>d__156>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.BattleScreen.<NetUseCardAsync>d__156&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.CombatChoreographer.<PerformAsync>d__76>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.CombatChoreographer.<PerformAsync>d__76&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.LobbyScreen.<ConnectAsync>d__94>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.LobbyScreen.<ConnectAsync>d__94&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleScreen.<NetPlaceLandAsync>d__160>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleScreen.<NetPlaceLandAsync>d__160&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleScreen.<NetSelectAsync>d__173>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleScreen.<NetSelectAsync>d__173&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleScreen.<NetTextAsync>d__175>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleScreen.<NetTextAsync>d__175&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleScreen.<SpellDeclarationFlow>d__75>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleScreen.<SpellDeclarationFlow>d__75&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleScreen.<StartDummyOpeningAsync>d__115>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleScreen.<StartDummyOpeningAsync>d__115&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.LobbyScreen.<CreateTapRoomAsync>d__93>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.LobbyScreen.<CreateTapRoomAsync>d__93&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.LobbyScreen.<JoinTapEntryAsync>d__131>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.LobbyScreen.<JoinTapEntryAsync>d__131&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.LobbyScreen.<RefreshTapRoomsAsync>d__129>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.LobbyScreen.<RefreshTapRoomsAsync>d__129&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.Online.TapAccountService.<WarmupInternal>d__20>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.Online.TapAccountService.<WarmupInternal>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.RankedMatchmaking.<ScanOnce>d__34>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.RankedMatchmaking.<ScanOnce>d__34&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.TapLoginBadge.<LoginFlow>d__17>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.TapLoginBadge.<LoginFlow>d__17&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleCellPicker.<LoopAsync>d__13>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleCellPicker.<LoopAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleFieldView.UnitVisual.<RollStatsAsync>d__32>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleFieldView.UnitVisual.<RollStatsAsync>d__32&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleHudRow.<PlayPhaseRingAsync>d__48>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleHudRow.<PlayPhaseRingAsync>d__48&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleResolutionDirector.<RunReplayAsync>d__26>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleResolutionDirector.<RunReplayAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleScreen.<NetEnsureFieldAppliedAsync>d__136>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleScreen.<NetEnsureFieldAppliedAsync>d__136&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleScreen.<RefreshFieldWhenBoardReady>d__59>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleScreen.<RefreshFieldWhenBoardReady>d__59&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleScreen.<StartReplayWhenBoardReadyAsync>d__185>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleScreen.<StartReplayWhenBoardReadyAsync>d__185&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleStageDirector.<SwallowToBattleAsync>d__24>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleStageDirector.<SwallowToBattleAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleTilePress.<RunAsync>d__10>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleTilePress.<RunAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleTutorialDirector.<AutoEndTurnAfterHealFxAsync>d__41>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleTutorialDirector.<AutoEndTurnAfterHealFxAsync>d__41&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleTutorialDirector.<ConsumeMidTransitionAsync>d__43>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleTutorialDirector.<ConsumeMidTransitionAsync>d__43&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleTutorialDirector.<PlayTutorialVoiceAsync>d__52>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleTutorialDirector.<PlayTutorialVoiceAsync>d__52&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleTutorialDirector.<ShowIntroPanelAfterCombatAsync>d__46>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleTutorialDirector.<ShowIntroPanelAfterCombatAsync>d__46&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleZoneProps.<PulseDeckAsync>d__28>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleZoneProps.<PulseDeckAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.CombatChoreographer.<PerformAsync>d__76>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.CombatChoreographer.<PerformAsync>d__76&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__45>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__45&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.CombatChoreographer.<TrackArrowsAsync>d__83>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.CombatChoreographer.<TrackArrowsAsync>d__83&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter<object>,SynergyUI.Online.TapAccountService.<WarmupInternal>d__20>(System.Runtime.CompilerServices.TaskAwaiter<object>&,SynergyUI.Online.TapAccountService.<WarmupInternal>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<CloudSave.CloudSyncService.CloudSyncHost.<TryAutoUpload>d__4>(CloudSave.CloudSyncService.CloudSyncHost.<TryAutoUpload>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<CloudSave.TapAccountService.<LoginFromUiAsync>d__10>(CloudSave.TapAccountService.<LoginFromUiAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<CloudSave.TapCloudSaveBootstrap.BootInitCallback.<RunAsync>d__2>(CloudSave.TapCloudSaveBootstrap.BootInitCallback.<RunAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleAiCastStage.<FadeDisposeArrowAsync>d__15>(SynergyUI.BattleAiCastStage.<FadeDisposeArrowAsync>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleCellPicker.<LoopAsync>d__13>(SynergyUI.BattleCellPicker.<LoopAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleDecisionHub.<NotChainCountdownAsync>d__62>(SynergyUI.BattleDecisionHub.<NotChainCountdownAsync>d__62&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleEffectFx.<LingerOrbAsync>d__25>(SynergyUI.BattleEffectFx.<LingerOrbAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleEffectFx.Runner.<LifeAsync>d__4>(SynergyUI.BattleEffectFx.Runner.<LifeAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleFieldView.<LingerCastingAsync>d__1>(SynergyUI.BattleFieldView.<LingerCastingAsync>d__1&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleFieldView.UnitVisual.<RollStatsAsync>d__32>(SynergyUI.BattleFieldView.UnitVisual.<RollStatsAsync>d__32&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleHudRow.<PlayPhaseRingAsync>d__48>(SynergyUI.BattleHudRow.<PlayPhaseRingAsync>d__48&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleOpeningDirector.<RunAsync>d__28>(SynergyUI.BattleOpeningDirector.<RunAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattlePlayhead.<FinishAsync>d__40>(SynergyUI.BattlePlayhead.<FinishAsync>d__40&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattlePlayhead.<RunPlaybackAsync>d__38>(SynergyUI.BattlePlayhead.<RunPlaybackAsync>d__38&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleResolutionDirector.<FinishAsync>d__27>(SynergyUI.BattleResolutionDirector.<FinishAsync>d__27&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleResolutionDirector.<RunReplayAsync>d__26>(SynergyUI.BattleResolutionDirector.<RunReplayAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<ActivateSkillAsync>d__105>(SynergyUI.BattleScreen.<ActivateSkillAsync>d__105&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<NetActivateSkillAsync>d__168>(SynergyUI.BattleScreen.<NetActivateSkillAsync>d__168&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<NetAttackFlowAsync>d__166>(SynergyUI.BattleScreen.<NetAttackFlowAsync>d__166&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<NetEnsureFieldAppliedAsync>d__136>(SynergyUI.BattleScreen.<NetEnsureFieldAppliedAsync>d__136&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<NetGuardFlowAsync>d__167>(SynergyUI.BattleScreen.<NetGuardFlowAsync>d__167&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<NetPlaceLandAsync>d__160>(SynergyUI.BattleScreen.<NetPlaceLandAsync>d__160&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<NetSelectAsync>d__173>(SynergyUI.BattleScreen.<NetSelectAsync>d__173&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<NetTextAsync>d__175>(SynergyUI.BattleScreen.<NetTextAsync>d__175&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<NetUseCardAsync>d__156>(SynergyUI.BattleScreen.<NetUseCardAsync>d__156&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<OnClickUnitEffectButton>d__33>(SynergyUI.BattleScreen.<OnClickUnitEffectButton>d__33&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<OnConcede>d__106>(SynergyUI.BattleScreen.<OnConcede>d__106&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<PickLandColorAsync>d__77>(SynergyUI.BattleScreen.<PickLandColorAsync>d__77&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<PromptModeThenPickCell>d__96>(SynergyUI.BattleScreen.<PromptModeThenPickCell>d__96&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<PulseAutoTargetGlowAsync>d__273>(SynergyUI.BattleScreen.<PulseAutoTargetGlowAsync>d__273&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<RefreshFieldWhenBoardReady>d__59>(SynergyUI.BattleScreen.<RefreshFieldWhenBoardReady>d__59&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<RejectDeckAndBackAsync>d__180>(SynergyUI.BattleScreen.<RejectDeckAndBackAsync>d__180&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<RunAiFirstTurnAsync>d__118>(SynergyUI.BattleScreen.<RunAiFirstTurnAsync>d__118&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<SpellDeclarationFlow>d__75>(SynergyUI.BattleScreen.<SpellDeclarationFlow>d__75&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<StartDummyOpeningAsync>d__115>(SynergyUI.BattleScreen.<StartDummyOpeningAsync>d__115&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<StartGauntletOpeningAsync>d__113>(SynergyUI.BattleScreen.<StartGauntletOpeningAsync>d__113&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<StartReplayWhenBoardReadyAsync>d__185>(SynergyUI.BattleScreen.<StartReplayWhenBoardReadyAsync>d__185&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleStageDirector.<MountBoardAndDawnAsync>d__25>(SynergyUI.BattleStageDirector.<MountBoardAndDawnAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleStageDirector.<SwallowToBattleAsync>d__24>(SynergyUI.BattleStageDirector.<SwallowToBattleAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleTilePress.<RunAsync>d__10>(SynergyUI.BattleTilePress.<RunAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleTutorialDirector.<AutoEndTurnAfterHealFxAsync>d__41>(SynergyUI.BattleTutorialDirector.<AutoEndTurnAfterHealFxAsync>d__41&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleTutorialDirector.<ConsumeMidTransitionAsync>d__43>(SynergyUI.BattleTutorialDirector.<ConsumeMidTransitionAsync>d__43&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleTutorialDirector.<PlayTutorialVoiceAsync>d__52>(SynergyUI.BattleTutorialDirector.<PlayTutorialVoiceAsync>d__52&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleTutorialDirector.<PromptGauntletOverAsync>d__70>(SynergyUI.BattleTutorialDirector.<PromptGauntletOverAsync>d__70&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleTutorialDirector.<PromptTutorialCompleteAsync>d__69>(SynergyUI.BattleTutorialDirector.<PromptTutorialCompleteAsync>d__69&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleTutorialDirector.<PromptTutorialRetryAsync>d__68>(SynergyUI.BattleTutorialDirector.<PromptTutorialRetryAsync>d__68&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleTutorialDirector.<ShowIntroPanelAfterCombatAsync>d__46>(SynergyUI.BattleTutorialDirector.<ShowIntroPanelAfterCombatAsync>d__46&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleVfxFx.<ReleaseSlotAfter>d__18>(SynergyUI.BattleVfxFx.<ReleaseSlotAfter>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleZoneProps.<PulseDeckAsync>d__28>(SynergyUI.BattleZoneProps.<PulseDeckAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.CardComposerScreen.<ConfirmForceDeleteCardAsync>d__141>(SynergyUI.CardComposerScreen.<ConfirmForceDeleteCardAsync>d__141&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.CombatChoreographer.<PerformAsync>d__76>(SynergyUI.CombatChoreographer.<PerformAsync>d__76&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__45>(SynergyUI.CombatChoreographer.<TargetingLoopAsync>d__45&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.CombatChoreographer.<TrackArrowsAsync>d__83>(SynergyUI.CombatChoreographer.<TrackArrowsAsync>d__83&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.DeckBuilderScreen.<ConfirmCopyPresetAsync>d__115>(SynergyUI.DeckBuilderScreen.<ConfirmCopyPresetAsync>d__115&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.DeckBuilderScreen.<ConfirmDeleteDeckAsync>d__117>(SynergyUI.DeckBuilderScreen.<ConfirmDeleteDeckAsync>d__117&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.EffectComposerScreen.<ConfirmForceDeleteEffectAsync>d__127>(SynergyUI.EffectComposerScreen.<ConfirmForceDeleteEffectAsync>d__127&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.EffectComposerScreen.<HandleReplacedLegacyAsync>d__128>(SynergyUI.EffectComposerScreen.<HandleReplacedLegacyAsync>d__128&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.LaunchScreen.<AutoAdvance>d__18>(SynergyUI.LaunchScreen.<AutoAdvance>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.LobbyScreen.<ConnectAsync>d__94>(SynergyUI.LobbyScreen.<ConnectAsync>d__94&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.LobbyScreen.<CreateTapRoomAsync>d__93>(SynergyUI.LobbyScreen.<CreateTapRoomAsync>d__93&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.LobbyScreen.<JoinTapEntryAsync>d__131>(SynergyUI.LobbyScreen.<JoinTapEntryAsync>d__131&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.LobbyScreen.<RefreshTapRoomsAsync>d__129>(SynergyUI.LobbyScreen.<RefreshTapRoomsAsync>d__129&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.LobbyScreen.<RunScanLoopAsync>d__123>(SynergyUI.LobbyScreen.<RunScanLoopAsync>d__123&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.Online.TapAccountService.<WarmupInternal>d__20>(SynergyUI.Online.TapAccountService.<WarmupInternal>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.RankedMatchmaking.<RunLoop>d__33>(SynergyUI.RankedMatchmaking.<RunLoop>d__33&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.RankedMatchmaking.<ScanOnce>d__34>(SynergyUI.RankedMatchmaking.<ScanOnce>d__34&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.TapLoginBadge.<LoginFlow>d__17>(SynergyUI.TapLoginBadge.<LoginFlow>d__17&)
		// Cysharp.Threading.Tasks.Internal.StateTuple<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>> Cysharp.Threading.Tasks.Internal.StateTuple.Create<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>)
		// Cysharp.Threading.Tasks.UniTask<byte> Cysharp.Threading.Tasks.UniTask.RunOnThreadPool<byte>(System.Func<byte>,bool,System.Threading.CancellationToken)
		// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<int,object,object>> Cysharp.Threading.Tasks.UniTask.WhenAny<object,object>(Cysharp.Threading.Tasks.UniTask<object>,Cysharp.Threading.Tasks.UniTask<object>)
		// Cysharp.Threading.Tasks.UniTask<object> Cysharp.Threading.Tasks.UniTaskExtensions.AsUniTask<object>(System.Threading.Tasks.Task<object>,bool)
		// Cysharp.Threading.Tasks.UniTask<object> Cysharp.Threading.Tasks.UniTaskExtensions.ContinueWith<object>(Cysharp.Threading.Tasks.UniTask,System.Func<object>)
		// System.Void Cysharp.Threading.Tasks.UniTaskExtensions.Forget<byte>(Cysharp.Threading.Tasks.UniTask<byte>)
		// Cysharp.Threading.Tasks.UniTask<object> Cysharp.Threading.Tasks.UniTaskExtensions.Timeout<object>(Cysharp.Threading.Tasks.UniTask<object>,System.TimeSpan,Cysharp.Threading.Tasks.DelayType,Cysharp.Threading.Tasks.PlayerLoopTiming,System.Threading.CancellationTokenSource)
		// System.Void MemoryPack.IMemoryPackFormatter<object>.Serialize<object>(MemoryPack.MemoryPackWriter<object>&,object&)
		// byte[] MemoryPack.Internal.MemoryMarshalEx.AllocateUninitializedArray<byte>(int,bool)
		// float[] MemoryPack.Internal.MemoryMarshalEx.AllocateUninitializedArray<float>(int,bool)
		// int[] MemoryPack.Internal.MemoryMarshalEx.AllocateUninitializedArray<int>(int,bool)
		// object[] MemoryPack.Internal.MemoryMarshalEx.AllocateUninitializedArray<object>(int,bool)
		// uint[] MemoryPack.Internal.MemoryMarshalEx.AllocateUninitializedArray<uint>(int,bool)
		// byte& MemoryPack.Internal.MemoryMarshalEx.GetArrayDataReference<byte>(byte[])
		// float& MemoryPack.Internal.MemoryMarshalEx.GetArrayDataReference<float>(float[])
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
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<float>(float[]&)
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<int>(int[]&)
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<object>(object[]&)
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<uint>(uint[]&)
		// byte[] MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<byte>()
		// float[] MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<float>()
		// int[] MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<int>()
		// uint[] MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<uint>()
		// MemoryPack.IMemoryPackFormatter<object> MemoryPack.MemoryPackReader.GetFormatter<object>()
		// System.Void MemoryPack.MemoryPackReader.ReadArray<object>(object[]&)
		// object[] MemoryPack.MemoryPackReader.ReadArray<object>()
		// System.Void MemoryPack.MemoryPackReader.ReadPackable<object>(object&)
		// object MemoryPack.MemoryPackReader.ReadPackable<object>()
		// System.Void MemoryPack.MemoryPackReader.ReadPackableArray<object>(object[]&)
		// object[] MemoryPack.MemoryPackReader.ReadPackableArray<object>()
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<byte,byte>(byte&,byte&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<byte,int,int,int,int,int>(byte&,int&,int&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<byte,int,int>(byte&,int&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<byte,uint,byte>(byte&,uint&,byte&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<byte>(byte&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<double>(double&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<float,int>(float&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<float>(float&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,byte,uint>(int&,byte&,uint&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,byte>(int&,byte&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,float>(int&,float&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,int,int,int,byte,uint,int,int,int,byte,byte>(int&,int&,int&,int&,byte&,uint&,int&,int&,int&,byte&,byte&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,int,int,int,int,int,byte>(int&,int&,int&,int&,int&,int&,byte&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,int,int,int>(int&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,int,int>(int&,int&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,int>(int&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,uint,ushort,long>(int&,uint&,ushort&,long&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,uint>(int&,uint&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int>(int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<long>(long&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<uint,int>(uint&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<uint>(uint&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<ushort>(ushort&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanagedArray<byte>(byte[]&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanagedArray<float>(float[]&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanagedArray<int>(int[]&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanagedArray<uint>(uint[]&)
		// byte[] MemoryPack.MemoryPackReader.ReadUnmanagedArray<byte>()
		// float[] MemoryPack.MemoryPackReader.ReadUnmanagedArray<float>()
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
		// System.Void MemoryPack.MemoryPackWriter<object>.DangerousWriteUnmanagedArray<float>(float[])
		// System.Void MemoryPack.MemoryPackWriter<object>.DangerousWriteUnmanagedArray<int>(int[])
		// System.Void MemoryPack.MemoryPackWriter<object>.DangerousWriteUnmanagedArray<object>(object[])
		// System.Void MemoryPack.MemoryPackWriter<object>.DangerousWriteUnmanagedArray<uint>(uint[])
		// MemoryPack.IMemoryPackFormatter<object> MemoryPack.MemoryPackWriter<object>.GetFormatter<object>()
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteArray<object>(object[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WritePackable<object>(object&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WritePackableArray<object>(object[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<byte,byte>(byte&,byte&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<byte,int,int,int,int,int>(byte&,int&,int&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<byte,int,int>(byte&,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<byte>(byte&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<double>(double&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<float,int>(float&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<float>(float&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,byte,uint>(int&,byte&,uint&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,byte>(int&,byte&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,int,int,int,byte,uint,int,int,int,byte,byte>(int&,int&,int&,int&,byte&,uint&,int&,int&,int&,byte&,byte&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,int,int>(int&,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,int>(int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,uint>(int&,uint&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int>(int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<long>(long&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<uint>(uint&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedArray<byte>(byte[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedArray<float>(float[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedArray<int>(int[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedArray<uint>(uint[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<byte,uint,byte>(byte,byte&,uint&,byte&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<byte>(byte,byte&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<float,int>(byte,float&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int,float>(byte,int&,float&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int,int,int,int,int,int,byte>(byte,int&,int&,int&,int&,int&,int&,byte&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int,int,int,int>(byte,int&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int,int,int>(byte,int&,int&,int&)
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
		// object System.Collections.Generic.CollectionExtensions.GetValueOrDefault<object,object>(System.Collections.Generic.IReadOnlyDictionary<object,object>,object)
		// object System.Collections.Generic.CollectionExtensions.GetValueOrDefault<object,object>(System.Collections.Generic.IReadOnlyDictionary<object,object>,object,object)
		// bool System.Enum.TryParse<int>(string,bool,int&)
		// bool System.Enum.TryParse<int>(string,int&)
		// bool System.Enum.TryParse<object>(string,bool,object&)
		// bool System.Enum.TryParse<object>(string,object&)
		// int System.HashCode.Combine<int,System.Nullable<int>>(int,System.Nullable<int>)
		// int System.HashCode.Combine<uint,System.DateTime>(uint,System.DateTime)
		// bool System.Linq.Enumerable.All<int>(System.Collections.Generic.IEnumerable<int>,System.Func<int,bool>)
		// bool System.Linq.Enumerable.All<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// bool System.Linq.Enumerable.Any<System.Collections.Generic.KeyValuePair<int,float>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,bool>)
		// bool System.Linq.Enumerable.Any<System.Collections.Generic.KeyValuePair<object,int>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>,System.Func<System.Collections.Generic.KeyValuePair<object,int>,bool>)
		// bool System.Linq.Enumerable.Any<System.ValueTuple<int,int>>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,int>>,System.Func<System.ValueTuple<int,int>,bool>)
		// bool System.Linq.Enumerable.Any<int>(System.Collections.Generic.IEnumerable<int>)
		// bool System.Linq.Enumerable.Any<int>(System.Collections.Generic.IEnumerable<int>,System.Func<int,bool>)
		// bool System.Linq.Enumerable.Any<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// bool System.Linq.Enumerable.Any<ushort>(System.Collections.Generic.IEnumerable<ushort>,System.Func<ushort,bool>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Cast<int>(System.Collections.IEnumerable)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Cast<object>(System.Collections.IEnumerable)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.CastIterator<int>(System.Collections.IEnumerable)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.CastIterator<object>(System.Collections.IEnumerable)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Concat<object>(System.Collections.Generic.IEnumerable<object>,System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.ConcatIterator<object>(System.Collections.Generic.IEnumerable<object>,System.Collections.Generic.IEnumerable<object>)
		// bool System.Linq.Enumerable.Contains<object>(System.Collections.Generic.IEnumerable<object>,object)
		// bool System.Linq.Enumerable.Contains<object>(System.Collections.Generic.IEnumerable<object>,object,System.Collections.Generic.IEqualityComparer<object>)
		// int System.Linq.Enumerable.Count<System.Collections.Generic.KeyValuePair<int,object>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>)
		// int System.Linq.Enumerable.Count<System.ValueTuple<int,float>>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,float>>,System.Func<System.ValueTuple<int,float>,bool>)
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
		// System.ValueTuple<int,object,object> System.Linq.Enumerable.First<System.ValueTuple<int,object,object>>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,object,object>>,System.Func<System.ValueTuple<int,object,object>,bool>)
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
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,float>> System.Linq.Enumerable.OrderBy<System.ValueTuple<int,float>,int>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,float>>,System.Func<System.ValueTuple<int,float>,int>)
		// System.Linq.IOrderedEnumerable<int> System.Linq.Enumerable.OrderBy<int,int>(System.Collections.Generic.IEnumerable<int>,System.Func<int,int>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,System.Guid>(System.Collections.Generic.IEnumerable<object>,System.Func<object,System.Guid>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,float>(System.Collections.Generic.IEnumerable<object>,System.Func<object,float>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,int>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>,System.Collections.Generic.IComparer<object>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,uint>(System.Collections.Generic.IEnumerable<object>,System.Func<object,uint>)
		// System.Linq.IOrderedEnumerable<System.Collections.Generic.KeyValuePair<int,float>> System.Linq.Enumerable.OrderByDescending<System.Collections.Generic.KeyValuePair<int,float>,float>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,float>)
		// System.Linq.IOrderedEnumerable<System.Collections.Generic.KeyValuePair<int,int>> System.Linq.Enumerable.OrderByDescending<System.Collections.Generic.KeyValuePair<int,int>,int>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>)
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>> System.Linq.Enumerable.OrderByDescending<System.ValueTuple<int,float,float>,float>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,float,float>>,System.Func<System.ValueTuple<int,float,float>,float>)
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,int>> System.Linq.Enumerable.OrderByDescending<System.ValueTuple<int,int>,int>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,int>>,System.Func<System.ValueTuple<int,int>,int>)
		// System.Linq.IOrderedEnumerable<int> System.Linq.Enumerable.OrderByDescending<int,float>(System.Collections.Generic.IEnumerable<int>,System.Func<int,float>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderByDescending<object,System.DateTime>(System.Collections.Generic.IEnumerable<object>,System.Func<object,System.DateTime>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderByDescending<object,byte>(System.Collections.Generic.IEnumerable<object>,System.Func<object,byte>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderByDescending<object,float>(System.Collections.Generic.IEnumerable<object>,System.Func<object,float>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderByDescending<object,int>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderByDescending<object,long>(System.Collections.Generic.IEnumerable<object>,System.Func<object,long>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderByDescending<object,uint>(System.Collections.Generic.IEnumerable<object>,System.Func<object,uint>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Reverse<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.ReverseIterator<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<SynergyUI.BattleDeckPickStage.Entry> System.Linq.Enumerable.Select<object,SynergyUI.BattleDeckPickStage.Entry>(System.Collections.Generic.IEnumerable<object>,System.Func<object,SynergyUI.BattleDeckPickStage.Entry>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<int,float>> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<int,float>,System.ValueTuple<int,float>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,System.ValueTuple<int,float>>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object,byte>> System.Linq.Enumerable.Select<System.ValueTuple<object,object,object>,System.ValueTuple<object,object,byte>>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object,object>>,System.Func<System.ValueTuple<object,object,object>,System.ValueTuple<object,object,byte>>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>> System.Linq.Enumerable.Select<System.ValueTuple<object,object>,System.ValueTuple<object,object>>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>,System.Func<System.ValueTuple<object,object>,System.ValueTuple<object,object>>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>> System.Linq.Enumerable.Select<object,System.ValueTuple<object,object>>(System.Collections.Generic.IEnumerable<object>,System.Func<object,System.ValueTuple<object,object>>)
		// System.Collections.Generic.IEnumerable<UnityEngine.Vector4> System.Linq.Enumerable.Select<int,UnityEngine.Vector4>(System.Collections.Generic.IEnumerable<int>,System.Func<int,UnityEngine.Vector4>)
		// System.Collections.Generic.IEnumerable<byte> System.Linq.Enumerable.Select<object,byte>(System.Collections.Generic.IEnumerable<object>,System.Func<object,byte>)
		// System.Collections.Generic.IEnumerable<double> System.Linq.Enumerable.Select<float,double>(System.Collections.Generic.IEnumerable<float>,System.Func<float,double>)
		// System.Collections.Generic.IEnumerable<float> System.Linq.Enumerable.Select<float,float>(System.Collections.Generic.IEnumerable<float>,System.Func<float,float>)
		// System.Collections.Generic.IEnumerable<float> System.Linq.Enumerable.Select<object,float>(System.Collections.Generic.IEnumerable<object>,System.Func<object,float>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<int,float>,int>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<int,int>,int>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Select<System.ValueTuple<int,int>,int>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,int>>,System.Func<System.ValueTuple<int,int>,int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Select<int,int>(System.Collections.Generic.IEnumerable<int>,System.Func<int,int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Select<object,int>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<int,float>,object>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<int,int>,object>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<object,int>,object>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>,System.Func<System.Collections.Generic.KeyValuePair<object,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.ValueTuple<int,object>,object>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,object>>,System.Func<System.ValueTuple<int,object>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.ValueTuple<object,int>,object>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,int>>,System.Func<System.ValueTuple<object,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.ValueTuple<object,object,byte>,object>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object,byte>>,System.Func<System.ValueTuple<object,object,byte>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.ValueTuple<object,object>,object>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>,System.Func<System.ValueTuple<object,object>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<Unity.InferenceEngine.Model.Input,object>(System.Collections.Generic.IEnumerable<Unity.InferenceEngine.Model.Input>,System.Func<Unity.InferenceEngine.Model.Input,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<int,object>(System.Collections.Generic.IEnumerable<int>,System.Func<int,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>)
		// System.Collections.Generic.IEnumerable<uint> System.Linq.Enumerable.Select<object,uint>(System.Collections.Generic.IEnumerable<object>,System.Func<object,uint>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.SelectIterator<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.SelectMany<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,System.Collections.Generic.IEnumerable<object>>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.SelectManyIterator<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,System.Collections.Generic.IEnumerable<object>>)
		// bool System.Linq.Enumerable.SequenceEqual<float>(System.Collections.Generic.IEnumerable<float>,System.Collections.Generic.IEnumerable<float>)
		// bool System.Linq.Enumerable.SequenceEqual<float>(System.Collections.Generic.IEnumerable<float>,System.Collections.Generic.IEnumerable<float>,System.Collections.Generic.IEqualityComparer<float>)
		// bool System.Linq.Enumerable.SequenceEqual<int>(System.Collections.Generic.IEnumerable<int>,System.Collections.Generic.IEnumerable<int>)
		// bool System.Linq.Enumerable.SequenceEqual<int>(System.Collections.Generic.IEnumerable<int>,System.Collections.Generic.IEnumerable<int>,System.Collections.Generic.IEqualityComparer<int>)
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
		// System.Linq.IOrderedEnumerable<System.Collections.Generic.KeyValuePair<int,int>> System.Linq.Enumerable.ThenBy<System.Collections.Generic.KeyValuePair<int,int>,int>(System.Linq.IOrderedEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>)
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
		// System.Collections.Generic.Dictionary<int,object> System.Linq.Enumerable.ToDictionary<object,int,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>,System.Func<object,object>)
		// System.Collections.Generic.Dictionary<int,object> System.Linq.Enumerable.ToDictionary<object,int,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>,System.Func<object,object>,System.Collections.Generic.IEqualityComparer<int>)
		// System.Collections.Generic.Dictionary<object,object> System.Linq.Enumerable.ToDictionary<object,object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>,System.Func<object,object>)
		// System.Collections.Generic.Dictionary<object,object> System.Linq.Enumerable.ToDictionary<object,object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>,System.Func<object,object>,System.Collections.Generic.IEqualityComparer<object>)
		// System.Collections.Generic.Dictionary<uint,object> System.Linq.Enumerable.ToDictionary<object,uint,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,uint>,System.Func<object,object>,System.Collections.Generic.IEqualityComparer<uint>)
		// System.Collections.Generic.Dictionary<uint,object> System.Linq.Enumerable.ToDictionary<object,uint>(System.Collections.Generic.IEnumerable<object>,System.Func<object,uint>)
		// System.Collections.Generic.HashSet<object> System.Linq.Enumerable.ToHashSet<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.HashSet<object> System.Linq.Enumerable.ToHashSet<object>(System.Collections.Generic.IEnumerable<object>,System.Collections.Generic.IEqualityComparer<object>)
		// System.Collections.Generic.List<SynergyUI.BattleDeckPickStage.Entry> System.Linq.Enumerable.ToList<SynergyUI.BattleDeckPickStage.Entry>(System.Collections.Generic.IEnumerable<SynergyUI.BattleDeckPickStage.Entry>)
		// System.Collections.Generic.List<System.ValueTuple<int,float>> System.Linq.Enumerable.ToList<System.ValueTuple<int,float>>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,float>>)
		// System.Collections.Generic.List<System.ValueTuple<int,int>> System.Linq.Enumerable.ToList<System.ValueTuple<int,int>>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,int>>)
		// System.Collections.Generic.List<System.ValueTuple<object,int,int>> System.Linq.Enumerable.ToList<System.ValueTuple<object,int,int>>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,int,int>>)
		// System.Collections.Generic.List<System.ValueTuple<object,object,byte>> System.Linq.Enumerable.ToList<System.ValueTuple<object,object,byte>>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object,byte>>)
		// System.Collections.Generic.List<System.ValueTuple<object,object>> System.Linq.Enumerable.ToList<System.ValueTuple<object,object>>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>)
		// System.Collections.Generic.List<float> System.Linq.Enumerable.ToList<float>(System.Collections.Generic.IEnumerable<float>)
		// System.Collections.Generic.List<int> System.Linq.Enumerable.ToList<int>(System.Collections.Generic.IEnumerable<int>)
		// System.Collections.Generic.List<object> System.Linq.Enumerable.ToList<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.List<uint> System.Linq.Enumerable.ToList<uint>(System.Collections.Generic.IEnumerable<uint>)
		// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>> System.Linq.Enumerable.Where<System.Collections.Generic.KeyValuePair<int,float>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,bool>)
		// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>> System.Linq.Enumerable.Where<System.Collections.Generic.KeyValuePair<int,int>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,bool>)
		// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>> System.Linq.Enumerable.Where<System.Collections.Generic.KeyValuePair<int,object>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>,System.Func<System.Collections.Generic.KeyValuePair<int,object>,bool>)
		// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>> System.Linq.Enumerable.Where<System.Collections.Generic.KeyValuePair<object,int>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>,System.Func<System.Collections.Generic.KeyValuePair<object,int>,bool>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<int,int>> System.Linq.Enumerable.Where<System.ValueTuple<int,int>>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,int>>,System.Func<System.ValueTuple<int,int>,bool>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<int,object>> System.Linq.Enumerable.Where<System.ValueTuple<int,object>>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,object>>,System.Func<System.ValueTuple<int,object>,bool>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>> System.Linq.Enumerable.Where<System.ValueTuple<object,object>>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>,System.Func<System.ValueTuple<object,object>,bool>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Where<int>(System.Collections.Generic.IEnumerable<int>,System.Func<int,bool>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Where<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Where<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int,bool>)
		// System.Collections.Generic.IEnumerable<uint> System.Linq.Enumerable.Where<uint>(System.Collections.Generic.IEnumerable<uint>,System.Func<uint,bool>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.WhereIterator<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int,bool>)
		// System.Collections.Generic.IEnumerable<SynergyUI.BattleDeckPickStage.Entry> System.Linq.Enumerable.Iterator<object>.Select<SynergyUI.BattleDeckPickStage.Entry>(System.Func<object,SynergyUI.BattleDeckPickStage.Entry>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<int,float>> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,float>>.Select<System.ValueTuple<int,float>>(System.Func<System.Collections.Generic.KeyValuePair<int,float>,System.ValueTuple<int,float>>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object,byte>> System.Linq.Enumerable.Iterator<System.ValueTuple<object,object,object>>.Select<System.ValueTuple<object,object,byte>>(System.Func<System.ValueTuple<object,object,object>,System.ValueTuple<object,object,byte>>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>> System.Linq.Enumerable.Iterator<System.ValueTuple<object,object>>.Select<System.ValueTuple<object,object>>(System.Func<System.ValueTuple<object,object>,System.ValueTuple<object,object>>)
		// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>> System.Linq.Enumerable.Iterator<object>.Select<System.ValueTuple<object,object>>(System.Func<object,System.ValueTuple<object,object>>)
		// System.Collections.Generic.IEnumerable<UnityEngine.Vector4> System.Linq.Enumerable.Iterator<int>.Select<UnityEngine.Vector4>(System.Func<int,UnityEngine.Vector4>)
		// System.Collections.Generic.IEnumerable<byte> System.Linq.Enumerable.Iterator<object>.Select<byte>(System.Func<object,byte>)
		// System.Collections.Generic.IEnumerable<double> System.Linq.Enumerable.Iterator<float>.Select<double>(System.Func<float,double>)
		// System.Collections.Generic.IEnumerable<float> System.Linq.Enumerable.Iterator<float>.Select<float>(System.Func<float,float>)
		// System.Collections.Generic.IEnumerable<float> System.Linq.Enumerable.Iterator<object>.Select<float>(System.Func<object,float>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,float>>.Select<int>(System.Func<System.Collections.Generic.KeyValuePair<int,float>,int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,int>>.Select<int>(System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Iterator<System.ValueTuple<int,int>>.Select<int>(System.Func<System.ValueTuple<int,int>,int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Iterator<int>.Select<int>(System.Func<int,int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Iterator<object>.Select<int>(System.Func<object,int>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,float>>.Select<object>(System.Func<System.Collections.Generic.KeyValuePair<int,float>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,int>>.Select<object>(System.Func<System.Collections.Generic.KeyValuePair<int,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<object,int>>.Select<object>(System.Func<System.Collections.Generic.KeyValuePair<object,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.ValueTuple<int,object>>.Select<object>(System.Func<System.ValueTuple<int,object>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.ValueTuple<object,int>>.Select<object>(System.Func<System.ValueTuple<object,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.ValueTuple<object,object,byte>>.Select<object>(System.Func<System.ValueTuple<object,object,byte>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.ValueTuple<object,object>>.Select<object>(System.Func<System.ValueTuple<object,object>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<Unity.InferenceEngine.Model.Input>.Select<object>(System.Func<Unity.InferenceEngine.Model.Input,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<int>.Select<object>(System.Func<int,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<object>.Select<object>(System.Func<object,object>)
		// System.Collections.Generic.IEnumerable<uint> System.Linq.Enumerable.Iterator<object>.Select<uint>(System.Func<object,uint>)
		// System.Linq.IOrderedEnumerable<System.Collections.Generic.KeyValuePair<int,int>> System.Linq.IOrderedEnumerable<System.Collections.Generic.KeyValuePair<int,int>>.CreateOrderedEnumerable<int>(System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>,System.Collections.Generic.IComparer<int>,bool)
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
		// System.Span<float> System.MemoryExtensions.AsSpan<float>(float[])
		// System.Span<int> System.MemoryExtensions.AsSpan<int>(int[])
		// System.Span<object> System.MemoryExtensions.AsSpan<object>(object[])
		// System.Span<uint> System.MemoryExtensions.AsSpan<uint>(uint[])
		// object System.Reflection.CustomAttributeExtensions.GetCustomAttribute<object>(System.Reflection.MemberInfo)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleScreen.<OnEndTurnFan>d__121>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleScreen.<OnEndTurnFan>d__121&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleScreen.<OnEndTurnFan>d__121>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleScreen.<OnEndTurnFan>d__121&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.Start<SynergyUI.BattleScreen.<OnEndTurnFan>d__121>(SynergyUI.BattleScreen.<OnEndTurnFan>d__121&)
		// bool System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<object>()
		// byte& System.Runtime.CompilerServices.Unsafe.Add<byte>(byte&,int)
		// byte& System.Runtime.CompilerServices.Unsafe.As<byte,byte>(byte&)
		// byte& System.Runtime.CompilerServices.Unsafe.As<float,byte>(float&)
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
		// float& System.Runtime.InteropServices.MemoryMarshal.GetReference<float>(System.Span<float>)
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