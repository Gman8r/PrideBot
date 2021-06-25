using PrideBot.Models;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace PrideBot.Repository
{
    public partial class ModelRepository
    {   
        public async Task<IEnumerable<SceneDialogue>> GetSceneDialogueForSceneAsync(SqlConnection conn, string sceneId)
        => (await new SqlCommand($"select * from VI_SCENE_DIALOGUES where SCENE_ID = '{sceneId}'", conn).ExecuteReaderAsync()).As<SceneDialogue>();
    }
}
