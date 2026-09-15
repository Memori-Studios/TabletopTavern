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
        SelectToBattle,
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
        Mage
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
    }
}
