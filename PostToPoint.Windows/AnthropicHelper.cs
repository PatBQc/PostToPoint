using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PostToPoint.Windows
{
    public class AnthropicHelper
    {
        public static async Task<string> CallClaude(string llmQuery, string llmChoice)
        {
            if (llmChoice != "claude-3-5-sonnet-latest")
            {
                throw new NotSupportedException(llmChoice + " is not supported, only claude-3-5-sonnet-latest supported for the moment.");
            }

            // You can load the API Key from an environment variable named ANTHROPIC_API_KEY by default.
            // TODO pass the value through the command line / UI as well
            var client = new AnthropicClient();

            var messages = new List<Anthropic.SDK.Messaging.Message>()
            {
                new Anthropic.SDK.Messaging.Message(RoleType.User, llmQuery)
            };

            // TODO configure those through config as well
            var parameters = new MessageParameters()
            {
                Messages = messages,
                MaxTokens = 1024 * 8,
                Model = AnthropicModels.Claude35Sonnet,
                Stream = false,
                Temperature = 0.4m,
            };

            var firstResult = await client.Messages.GetClaudeMessageAsync(parameters);

            return firstResult.Message.ToString();
        }
    }
}
