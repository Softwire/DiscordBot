using System;

namespace DiscordBot.DataAccess
{
    internal class SheetsColumn
    {
        public int Index { get; }
        public char Letter { get; }

        public SheetsColumn(int index)
        {
            Index = index;
            Letter = Convert.ToChar(index + 65);
        }
    }
}
