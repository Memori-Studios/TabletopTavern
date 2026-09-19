/// <summary>
/// How many unit roots currently carry each outline bit, so the renderer feature can skip states
/// with nothing to draw. Written by UnitOutlineSystem, read by UnitOutlineFeature.
/// </summary>
public static class UnitOutlineState
{
    public static int HoverPlayerCount;
    public static int HoverEnemyCount;
    public static int SelectedCount;

    public static uint ActiveMask =>
        (HoverPlayerCount > 0 ? TabletopTavernConstants.OUTLINE_LAYER_HOVER_PLAYER : 0)
        | (HoverEnemyCount > 0 ? TabletopTavernConstants.OUTLINE_LAYER_HOVER_ENEMY : 0)
        | (SelectedCount > 0 ? TabletopTavernConstants.OUTLINE_LAYER_SELECTED : 0);

    public static void Reset()
    {
        HoverPlayerCount = 0;
        HoverEnemyCount = 0;
        SelectedCount = 0;
    }

    // Counts follow the first mesh of a root, so a root that changed from before to after moves each bit's count by one.
    public static void Track(uint before, uint after)
    {
        HoverPlayerCount += Delta(before, after, TabletopTavernConstants.OUTLINE_LAYER_HOVER_PLAYER);
        HoverEnemyCount += Delta(before, after, TabletopTavernConstants.OUTLINE_LAYER_HOVER_ENEMY);
        SelectedCount += Delta(before, after, TabletopTavernConstants.OUTLINE_LAYER_SELECTED);
    }

    private static int Delta(uint before, uint after, uint bit)
    {
        bool was = (before & bit) != 0;
        bool now = (after & bit) != 0;
        return was == now ? 0 : (now ? 1 : -1);
    }
}
