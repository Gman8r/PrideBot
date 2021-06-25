using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class SceneDialogue
    {
        [PrimaryKey]
        public int SceneDialogueId { get; set; }
        public string SceneId { get; set; }
        public string Action { get; set; }
        public int TypingTime { get; set; }
        public int ReadTime { get; set; }
        public string ClientId { get; set; }
        public string Content { get; set; }
        public string Attachment { get; set; }
    }
}
