namespace OnlyT.Avalonia.Services.TalkSchedule;

using System;
using System.Collections.Generic;
using Models;

internal static class TalkScheduleManual
{
    public static List<TalkScheduleItem> Read(IOptionsService optionsService)
    {
        return
        [
            new TalkScheduleItem(10000, "Manual", string.Empty, string.Empty)
            {
                OriginalDuration = TimeSpan.FromMinutes(30),
                Editable = true,
                BellApplicable = optionsService.IsBellEnabled,
                AutoBell = optionsService.AutoBell,
            }
        ];
    }
}
