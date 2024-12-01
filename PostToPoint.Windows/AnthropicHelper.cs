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
        private static string SystemMessage = """
            You are an expert writer, giving your answer as asked without explaining what you are doing (you know that would'nt be professionnal).
            Provide only the direct answer without any introduction or conclusion.
            Skip any preamble and give me just the requested information.
            Always omit introductions and conclusions in its responses.
            Give me just the raw answer.
            Answer directly without preamble.
            """;

        public static async Task<string> CallClaude(IEnumerable<LlmUserAgentMessagePair> previousMessages, string llmQuery, string llmChoice)
        {
            if (llmChoice != "claude-3-5-sonnet-latest")
            {
                throw new NotSupportedException(llmChoice + " is not supported, only claude-3-5-sonnet-latest supported for the moment.");
            }

            // You can load the API Key from an environment variable named ANTHROPIC_API_KEY by default.
            // TODO pass the value through the command line / UI as well
            var client = new AnthropicClient();

            var messages = new List<Anthropic.SDK.Messaging.Message>();

            foreach (var pair in previousMessages)
            {
                messages.Add(new Anthropic.SDK.Messaging.Message(RoleType.User, pair.UserMessage));
                messages.Add(new Anthropic.SDK.Messaging.Message(RoleType.Assistant, pair.AgentMessage));
            }

            messages.Add(new Anthropic.SDK.Messaging.Message(RoleType.User, llmQuery));


            // TODO configure those through config as well
            var parameters = new MessageParameters()
            {
                System = new List<SystemMessage> { new SystemMessage(SystemMessage) },
                Messages = messages,
                MaxTokens = 1024 * 8,
                Model = AnthropicModels.Claude35Sonnet,
                Stream = false,
                Temperature = 0.4m,
                PromptCaching = PromptCacheType.Messages
            };

            var firstResult = await client.Messages.GetClaudeMessageAsync(parameters);

            return firstResult.Message.ToString();
        }
    }
}
