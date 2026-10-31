using UnityEngine;

/// <summary>
/// Fills a fixed row of slots from a list of currency amounts. Slots are placed in
/// the scene rather than spawned, so there are usually more of them than a given
/// payout needs — the spares are switched off.
/// </summary>
public static class CurrencySlots
{
    public static void Fill(BaseSlotInfo[] slots, CurrencyAmount[] amounts, Color color)
    {
        if (slots == null) return;

        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            var used = amounts != null && i < amounts.Length && amounts[i].Currency != null;
            slots[i].gameObject.SetActive(used);
            if (!used) continue;

            slots[i].SetInfoText(amounts[i].Amount);
            slots[i].SetInfoTextColor(color);
            slots[i].SetIconColor(Color.white);

            if (amounts[i].Currency.Icon != null) slots[i].SetIcon(amounts[i].Currency.Icon);
        }
    }
}
