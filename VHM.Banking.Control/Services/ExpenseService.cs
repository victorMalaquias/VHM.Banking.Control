using Microsoft.EntityFrameworkCore;
using OpenAI;
using OpenAI.Assistants;
using OpenAI.Files;
using System.Globalization;
using System.Text;
using System.Text.Json;
using VHM.Banking.Control.Data;
using VHM.Banking.Control.Entities;
using VHM.Banking.Control.Repositories.Interfaces;

namespace VHM.Banking.Control.Services
{
    public class ExpenseService
    {
        private readonly IExpenseRepository _expenseRepository;
        private readonly ApplicationDbContext _context;

        public ExpenseService(IExpenseRepository expenseRepository, ApplicationDbContext context)
        {
            _expenseRepository = expenseRepository;
            _context = context;
        }

        public async Task AddExpenseAsync(Expense expense)
        {
            if (expense.Amount <= 0)
                throw new ArgumentException("The expense amount must be positive.");

            if (string.IsNullOrWhiteSpace(expense.Description))
                throw new ArgumentException("The expense description cannot be empty.");

            await _expenseRepository.AddExpenseAsync(expense);
        }

        public async Task GenerateGraph(GraphRequest request)
        {
            try
            {
                var monthNumber = DateTime.ParseExact(request.Month, "MMMM", CultureInfo.InvariantCulture).Month;

                var expenses = await _context.Expenses
                    .Where(e => e.Category == request.Category && e.Date.Month == monthNumber)
                    .ToListAsync();

                if (expenses == null || expenses.Count == 0)
                {
                    throw new Exception("No expenses found for the given category and month.");
                }

                var expenseData = new
                {
                    description = $"Expenses for {request.Category} in {request.Month}",
                    expenses = expenses.Select(e => new
                    {
                        e.Description,
                        e.Amount
                    })
                };

                string jsonData = JsonSerializer.Serialize(expenseData);

                OpenAIClient openAIClient = new("Sua chave de API AQUI");

#pragma warning disable OPENAI001
                var assistantClient = openAIClient.GetAssistantClient();
                var fileClient = openAIClient.GetOpenAIFileClient();

                using Stream document = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));
                OpenAIFile expenseFile = await fileClient.UploadFileAsync(document, "expense_data.json", FileUploadPurpose.Assistants);

                var assistantOptions = new AssistantCreationOptions
                {
                    Name = "Expense Graph Generator",
                    Instructions = "You are an assistant that uses the provided JSON data to generate a graph.  Use the code interpreter tool to generate a graph of the expenses.", // Instrução mais clara
                    Tools = { new CodeInterpreterToolDefinition() },
                    ToolResources = new()
                    {
                        FileSearch = new()
                        {
                            NewVectorStores =
                            {
                                new VectorStoreCreationHelper([expenseFile.Id]),
                            }
                        }
                    },
                };

                Assistant assistant = await assistantClient.CreateAssistantAsync("gpt-4o", assistantOptions);

                ThreadCreationOptions threadOptions = new()
                {
                    InitialMessages = { $"Generate a graph for the expenses in {request.Month} based on the provided file." }
                };

                ThreadRun threadRun = await assistantClient.CreateThreadAndRunAsync(assistant.Id, threadOptions);

                do
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    threadRun = await assistantClient.GetRunAsync(threadRun.ThreadId, threadRun.Id);
                }
                while (!threadRun.Status.IsTerminal);

                var messages = assistantClient.GetMessagesAsync(
                    threadRun.ThreadId,
                    new MessageCollectionOptions() { Order = MessageCollectionOrder.Ascending });

                await foreach (ThreadMessage message in messages)
                {
                    foreach (MessageContent contentItem in message.Content)
                    {
                        if (!string.IsNullOrEmpty(contentItem.Text))
                        {
                            Console.WriteLine($"[ASSISTANT]: {contentItem.Text}");
                        }

                        if (!string.IsNullOrEmpty(contentItem.ImageFileId))
                        {
                            OpenAIFile imageInfo = await fileClient.GetFileAsync(contentItem.ImageFileId);
                            BinaryData imageBytes = await fileClient.DownloadFileAsync(contentItem.ImageFileId);

                            string directoryPath = @"C:\Users\victo\Desktop\BankingControl\";
                            Directory.CreateDirectory(directoryPath);

                            string filePath = Path.Combine(directoryPath, $"{imageInfo.Filename}");

                            using FileStream stream = File.OpenWrite(filePath);
                            await imageBytes.ToStream().CopyToAsync(stream);

                            Console.WriteLine($"Image saved to: {filePath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao gerar gráfico: {ex.Message}");
                throw;
            }
        }


        public async Task<IEnumerable<Expense>> GetAllExpensesAsync()
        {
            return await _expenseRepository.GetAllExpensesAsync();
        }

        public async Task UpdateExpenseAsync(Expense expense)
        {
            if (expense.Amount <= 0)
                throw new ArgumentException("The expense amount must be positive.");

            if (string.IsNullOrWhiteSpace(expense.Description))
                throw new ArgumentException("The expense description cannot be empty.");

            await _expenseRepository.UpdateExpenseAsync(expense);
        }

        public async Task DeleteExpenseAsync(int id)
        {
            await _expenseRepository.DeleteExpenseAsync(id);
        }
    }
}
