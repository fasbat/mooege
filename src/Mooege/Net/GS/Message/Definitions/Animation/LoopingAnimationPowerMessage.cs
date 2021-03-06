﻿/*
 * Copyright (C) 2011 mooege project
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */

using System.Text;

namespace Mooege.Net.GS.Message.Definitions.Inventory
{
    [Message( Opcodes.LoopingAnimationPowerMessage)]
    class LoopingAnimationPowerMessage : GameMessage
    {
        public int snoPower;
        public int snoData0;
        public int Field2;

        public override void Parse(GameBitBuffer buffer)
        {
            snoPower = buffer.ReadInt(32);
            snoData0 = buffer.ReadInt(32);
            Field2 = buffer.ReadInt(32);
        }

        public override void Encode(GameBitBuffer buffer)
        {
            buffer.WriteInt(32, snoPower);
            buffer.WriteInt(32, snoData0);
            buffer.WriteInt(32, Field2);
        }

        public override void AsText(StringBuilder b, int pad)
        {
            b.Append(' ', pad);
            b.AppendLine("LoopingAnimationPowerMessage:");
            b.Append(' ', pad++);
            b.AppendLine("{");
            b.Append(' ', pad); b.AppendLine("snoPower: 0x" + snoPower.ToString("X8") + " (" + snoPower + ")");
            b.Append(' ', pad); b.AppendLine("snoData0: 0x" + snoData0.ToString("X8") + " (" + snoData0 + ")");
            b.Append(' ', pad); b.AppendLine("Field2: 0x" + Field2.ToString("X8") + " (" + Field2 + ")");
            b.Append(' ', --pad);
            b.AppendLine("}");
        }
    }
}
