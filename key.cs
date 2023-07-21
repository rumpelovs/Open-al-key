using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

class Program
{
    static async Task Main(string[] args)
    {
        // Проверяем подключение к интернету
        try
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync("https://www.google.com/");
                response.EnsureSuccessStatusCode();
            }
        }
        catch (HttpRequestException)
        {
            Console.WriteLine("Отсутствует интернет-соединение. Проверьте подключение и попробуйте еще раз.");
            return;
        }

        // Переименовываем файл с ключами
        string oldFilename = "keys.txt";
        string newFilename = "openai_api_keys.txt";
        if (File.Exists(oldFilename))
        {
            File.Move(oldFilename, newFilename);
        }

        // Создаем файл с ключами, если его нет
        if (!File.Exists(newFilename))
        {
            File.Create(newFilename).Dispose();
        }

        // Генерируем новые ключи и записываем их в файл
        string[] GenerateKeys()
        {
            string[] keys = new string[3];
            for (int i = 0; i < 3; i++)
            {
                byte[] data = new byte[8];
                using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
                {
                    rng.GetBytes(data);
                }
                keys[i] = BitConverter.ToString(data).Replace("-", "").ToLower();
            }
            File.WriteAllLines(newFilename, keys);
            return keys;
        }

        // Получаем ключи из файла
        string[] GetKeys()
        {
            return File.ReadAllLines(newFilename);
        }

        // Отправляем запрос на API с использованием ключа
        async Task<string> SendRequest(string prompt, string model, string apiKey)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    var json = JsonConvert.SerializeObject(new { prompt });
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    var response = await client.PostAsync($"https://api.openai.com/v1/{model}/completions", content);
                    response.EnsureSuccessStatusCode();
                    var responseJson = await response.Content.ReadAsStringAsync();
                    dynamic responseObject = JsonConvert.DeserializeObject(responseJson);
                    return responseObject.choices[0].text;
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("Ошибка при отправке запроса на API OpenAI: " + e.Message);
                return null;
            }
            catch (JsonException)
            {
                Console.WriteLine("Ошибка при обработке ответа от API OpenAI");
                return null;
            }
        }

        // Проверяем ключи на работоспособность
        async Task<string[]> TestKeys()
        {
            string[] keys = GetKeys();
            foreach (string key in keys)
            {
                string response = await SendRequest("test", "davinci", key);
                if (response != null)
                {
                    Console.WriteLine("Найдены новые ключи API OpenAI: " + string.Join(", ", keys));
                    return keys;
                }
                else
                {
                    Console.WriteLine("Ключ " + key + " не работает. Попробуйте другой ключ.");
                    await Task.Delay(1000);
                }
            }
            return null;
        }

        // Получаем ключи и тестируем их
        string[] keys = GetKeys();
        if (keys.Length == 0)
        {
            keys = GenerateKeys();
        }
        while (true)
        {
            string[] newKeys = await TestKeys();
            if (newKeys != null)
            {
                keys = newKeys;
                break;
            }
            else
            {
                Console.WriteLine("Не удалось получить доступ к API OpenAI. Попробуйте еще раз через 1 секунд.");
                await Task.Delay(1000);
            }
        }

        // Используем работающий ключ для отправки запроса на API
        string response = await SendRequest("Hello, world!", "davinci", keys[0]);
        Console.WriteLine(response);
    }
}