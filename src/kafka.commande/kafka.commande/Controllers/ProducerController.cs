using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using kafka.commande.messages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace kafka.commande.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProducerController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IKafkaService _kafkaService;

        public ProducerController(IConfiguration configuration, IKafkaService kafkaService)
        {
            _configuration = configuration;
            _kafkaService = kafkaService;
        }

        // GET Producer/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return _kafkaService.ProduceMessage(id);
        }
    }
}
