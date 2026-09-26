namespace TJ
{
    [System.Serializable] public struct TutorialStep
    {
        public int stepID;
        public TutorialStepEnum tutorialStepEnum;
    }
    [System.Serializable] public enum TutorialStepEnum
    {
        Blank,
        SelectNode,
        SelectToBattle, // No step uses it; kept because the ordinals after it are serialized.
        PrestigeUnit,
        DisbandUnit,
        SellGear,
        ReorderUnits,
        Autoresolve,
        ModifyRoll,
        HealthRecovery,
        MoveCamera,
        RotateCamera,
        SelectUnit,
        RepositionUnit,
        GiveAttackOrders,
        SelectMultipleUnits,
        ChangeBattleSpeed,
        StartBattle,
        RainWeather,
        TownExplanation,
        Artillery,
        SignatureUnitPacks,
        GoldInterest,
        EventExplanation,
        ConsumableUsage,
        Mage,
        EnemyArtillery,
        PostBattleChoices,
        EnemyOutriders,
        FogWeather,
        SnowWeather,
        FreeCamera,
        CeaseFire,
        RenownCarriesOver,
        RenameSquad
    }
    public static class TutorialData
    {
        public static TutorialStep SelectNode = new()
        {
            tutorialStepEnum = TutorialStepEnum.SelectNode,
            stepID = 0,
        };
        public static TutorialStep PrestigeUnit = new()
        {
            tutorialStepEnum = TutorialStepEnum.PrestigeUnit,
            stepID = 2,
        };
        public static TutorialStep DisbandUnit = new()
        {
            tutorialStepEnum = TutorialStepEnum.DisbandUnit,
            stepID = 3,
        };
        public static TutorialStep SellGear = new()
        {
            tutorialStepEnum = TutorialStepEnum.SellGear,
            stepID = 4,
        };
        public static TutorialStep ReorderUnits = new()
        {
            tutorialStepEnum = TutorialStepEnum.ReorderUnits,
            stepID = 5,
        };
        public static TutorialStep Autoresolve = new()
        {
            tutorialStepEnum = TutorialStepEnum.Autoresolve,
            stepID = 6,
        };
        public static TutorialStep ModifyRoll = new()
        {
            tutorialStepEnum = TutorialStepEnum.ModifyRoll,
            stepID = 7,
        };
        public static TutorialStep HealthRecovery = new()
        {
            tutorialStepEnum = TutorialStepEnum.HealthRecovery,
            stepID = 9,
        };
        public static TutorialStep MoveCamera = new()
        {
            tutorialStepEnum = TutorialStepEnum.MoveCamera,
            stepID = 10,
        };
        public static TutorialStep RotateCamera = new()
        {
            tutorialStepEnum = TutorialStepEnum.RotateCamera,
            stepID = 11,
        };
        public static TutorialStep SelectUnit = new()
        {
            tutorialStepEnum = TutorialStepEnum.SelectUnit,
            stepID = 12,
        };
        public static TutorialStep RepositionUnit = new()
        {
            tutorialStepEnum = TutorialStepEnum.RepositionUnit,
            stepID = 13,
        };
        public static TutorialStep GiveAttackOrders = new()
        {
            tutorialStepEnum = TutorialStepEnum.GiveAttackOrders,
            stepID = 14,
        };
        public static TutorialStep SelectMultipleUnits = new()
        {
            tutorialStepEnum = TutorialStepEnum.SelectMultipleUnits,
            stepID = 15,
        };
        public static TutorialStep ChangeBattleSpeed = new()
        {
            tutorialStepEnum = TutorialStepEnum.ChangeBattleSpeed,
            stepID = 16,
        };
        public static TutorialStep StartBattle = new()
        {
            tutorialStepEnum = TutorialStepEnum.StartBattle,
            stepID = 17,
        };
        public static TutorialStep RainWeather = new()
        {
            tutorialStepEnum = TutorialStepEnum.RainWeather,
            stepID = 18,
        };
        public static TutorialStep TownExplanation = new ()
        {
            tutorialStepEnum = TutorialStepEnum.TownExplanation,
            stepID = 21,
        };
        public static TutorialStep Artillery = new()
        {
            tutorialStepEnum = TutorialStepEnum.Artillery,
            stepID = 22,
        };
        public static TutorialStep SignatureUnitPacks = new()
        {
            tutorialStepEnum = TutorialStepEnum.SignatureUnitPacks,
            stepID = 23,
        };
        public static TutorialStep GoldInterest = new()
        {
            tutorialStepEnum = TutorialStepEnum.GoldInterest,
            stepID = 26,
        };
        public static TutorialStep EventExplanation = new()
        {
            tutorialStepEnum = TutorialStepEnum.EventExplanation,
            stepID = 27,
        };
        public static TutorialStep ConsumableUsage = new()
        {
            tutorialStepEnum = TutorialStepEnum.ConsumableUsage,
            stepID = 28,
        };
        // stepIDs 1, 8, 19, 20, 24 and 25 are gaps left by deleted steps. Do not reuse them: a save that
        // completed the old step would silently skip the new one. Append past the highest id instead.
        public static TutorialStep Mage = new()
        {
            tutorialStepEnum = TutorialStepEnum.Mage,
            stepID = 29,
        };
        // Engagement panel, first time the enemy roster contains artillery.
        public static TutorialStep EnemyArtillery = new()
        {
            tutorialStepEnum = TutorialStepEnum.EnemyArtillery,
            stepID = 30,
        };
        // Engagement panel, first time the player claims rewards after a win.
        public static TutorialStep PostBattleChoices = new()
        {
            tutorialStepEnum = TutorialStepEnum.PostBattleChoices,
            stepID = 31,
        };
        // Engagement panel, first time the enemy roster contains Outriders.
        public static TutorialStep EnemyOutriders = new()
        {
            tutorialStepEnum = TutorialStepEnum.EnemyOutriders,
            stepID = 32,
        };
        public static TutorialStep FogWeather = new()
        {
            tutorialStepEnum = TutorialStepEnum.FogWeather,
            stepID = 33,
        };
        public static TutorialStep SnowWeather = new()
        {
            tutorialStepEnum = TutorialStepEnum.SnowWeather,
            stepID = 34,
        };
        // Callout on the map HUD's free camera button, after the first node is completed.
        public static TutorialStep FreeCamera = new()
        {
            tutorialStepEnum = TutorialStepEnum.FreeCamera,
            stepID = 35,
        };
        // Callout on the battle Cease Fire button, first time a shooter is selected.
        public static TutorialStep CeaseFire = new()
        {
            tutorialStepEnum = TutorialStepEnum.CeaseFire,
            stepID = 36,
        };
        // Game Over screen, first run end: Renown is kept and spent under Upgrades.
        public static TutorialStep RenownCarriesOver = new()
        {
            tutorialStepEnum = TutorialStepEnum.RenownCarriesOver,
            stepID = 37,
        };
        // Map HUD, first time a single unit card is selected while no other tip is showing.
        public static TutorialStep RenameSquad = new()
        {
            tutorialStepEnum = TutorialStepEnum.RenameSquad,
            stepID = 38,
        };
    }
}
