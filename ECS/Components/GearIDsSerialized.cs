using TJ;

[System.Serializable]
public struct GearIDsSerialized
{
    public GearID gearID1;
    public GearID gearID2;
    public GearID gearID3;
    public GearID gearID4;
    public GearID gearID5;

    // Slot 5 is a Renown unlock; a reader that stops at slot 4 drops that gear in battle.
    public bool Contains(GearID gearID)
    {
        return gearID1 == gearID || gearID2 == gearID || gearID3 == gearID || gearID4 == gearID || gearID5 == gearID;
    }
}
