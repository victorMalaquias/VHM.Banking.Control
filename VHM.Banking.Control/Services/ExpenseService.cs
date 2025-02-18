using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Files;
using OpenAI.Assistants;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VHM.Banking.Control.Data;
using Microsoft.EntityFrameworkCore;
using VHM.Banking.Control.Data;
using VHM.Banking.Control.Entities;
using VHM.Banking.Control.Repositories.Interfaces;
using System.Globalization;

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

            var connectionString = "your-azure-openai-endpoint";
            var azureAIClient = new AzureOpenAIClient(new Uri(connectionString), new DefaultAzureCredential());
#pragma warning disable OPENAI001
            var assistantClient = azureAIClient.GetAssistantClient();
            var fileClient = azureAIClient.GetOpenAIFileClient();

            using Stream document = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonData));

            OpenAIFile expenseFile = await fileClient.UploadFileAsync(
                document,
                "expense_data.json",
                FileUploadPurpose.Assistants);

            var assistantOptions = new AssistantCreationOptions
            {
                Name = "Expense Graph Generator",
                Instructions = "Use the data provided to generate a graph for expenses.",
                Tools = { new CodeInterpreterToolDefinition() },
                ToolResources = new()
                {
                    FileSearch = new()
                    {
                        NewVectorStores = { new VectorStoreCreationHelper([expenseFile.Id]) }
                    }
                }
            };

            Assistant assistant = assistantClient.CreateAssistant("gpt-4o", assistantOptions);

            var threadOptions = new ThreadCreationOptions
            {
                InitialMessages = { "Generate a graph for the expenses." }
            };

            ThreadRun threadRun = assistantClient.CreateThreadAndRun(assistant.Id, threadOptions);

            do
            {
                await Task.Delay(1000);
                threadRun = await assistantClient.GetRunAsync(threadRun.ThreadId, threadRun.Id);
            }
            while (!threadRun.Status.IsTerminal);

            var messages = assistantClient.GetMessagesAsync(
                 threadRun.ThreadId,
                 new MessageCollectionOptions()
                 {
                     Order = MessageCollectionOrder.Ascending
                 });

            await foreach (ThreadMessage message in messages)
            {
                Console.Write($"[{message.Role.ToString().ToUpper()}]: ");
                foreach (MessageContent contentItem in message.Content)
                {
                    if (!string.IsNullOrEmpty(contentItem.Text))
                    {
                        Console.WriteLine($"{contentItem.Text}");

                        if (contentItem.TextAnnotations.Count > 0)
                        {
                            Console.WriteLine();
                        }

                        foreach (TextAnnotation annotation in contentItem.TextAnnotations)
                        {
                            if (!string.IsNullOrEmpty(annotation.InputFileId))
                            {
                                Console.WriteLine($"* File citation, file ID: {annotation.InputFileId}");
                            }
                            if (!string.IsNullOrEmpty(annotation.OutputFileId))
                            {
                                Console.WriteLine($"* File output, new file ID: {annotation.OutputFileId}");
                            }
                        }
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
                Console.WriteLine();
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
