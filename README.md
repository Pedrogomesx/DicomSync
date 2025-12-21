# 🏥 DicomSync - Editor e Envio DICOM

![Status do Projeto](https://img.shields.io/badge/Status-Estável-green)
![.NET](https://img.shields.io/badge/.NET-WPF-purple)
![Arquitetura](https://img.shields.io/badge/Arquitetura-MVVM--Clean-blue)

**DicomSync** é uma ferramenta robusta para gestão, edição técnica e sincronização de exames DICOM. Desenvolvida com foco em performance e confiabilidade, a aplicação isola a complexidade do protocolo DICOM em uma arquitetura moderna, facilitando a correção de metadados e o envio para servidores PACS.

---

## 📸 Screenshots

<div align="center">
  <img width="45%" src="https://github.com/user-attachments/assets/3752a730-8ccb-4980-85be-91f32f496ac6" />
  <img width="45%" src="https://github.com/user-attachments/assets/ef6f2b9b-ed55-425b-8a64-2a20cf2da188" />
</div>

---

## 🚀 Funcionalidades Principais

### 1. Gestão e Visualização
* **Varredura Inteligente:** Localização recursiva de arquivos DICOM com validação de cabeçalho.
* **Agrupamento Automático:** Visualização organizada por Séries ou instâncias individuais.
* **UX Brasileira:** Máscaras de data automáticas (`dd/mm/yyyy`) com conversão transparente para o padrão DICOM (`yyyyMMdd`).

### 2. ✏️ DATAMAKER (Edição e Segurança)
* **Edição em Lote:** Sincronização de alterações em todos os arquivos do estudo simultaneamente.
* **Anonimização:** Função para descaracterizar estudos (remover nomes, datas e IDs sensíveis).
* **Backup Preventivo:** Criação automática da pasta `BACKUP_ORIGINAL` antes de qualquer modificação física nos arquivos.
* **Processamento Assíncrono:** Operações de I/O realizadas em segundo plano para manter a interface fluida.

### 3. 📡 Conectividade PACS
* **C-ECHO Multinível:** Teste de ping DICOM validando tanto a porta TCP quanto a aceitação do AE Title.
* **C-STORE Robusto:** Motor de envio com tratamento de erros amigável e logs técnicos detalhados.
* **Feedback em Tempo Real:** Acompanhamento de sucessos e falhas por meio de contadores e barras de progresso.

---

## 🏗️ Arquitetura e Engenharia

O projeto foi refatorado utilizando princípios de **Clean Architecture** e **MVVM**, garantindo manutenibilidade e portabilidade:

* **Services:** Isola a biblioteca `fo-dicom` e a lógica de rede.
* **ViewModels:** Gerencia o estado da UI e a lógica de apresentação.
* **Helpers:** Centraliza formatações complexas (Datas, Tags, etc).
* **Single-File Ready:** Configurado para publicação como executável único (Self-contained), funcionando sem necessidade de instalação do .NET no cliente.

---

## 🛠️ Tecnologias Utilizadas

* **C# / WPF** (.NET Desktop)
* **[fo-dicom](https://github.com/fo-dicom/fo-dicom):** Versão 5.0+ (Utilizando `DicomClientFactory`).
* **Multi-threading:** Uso intensivo de `Task.Run` e `Dispatcher` para operações de longa duração.

---

## 📦 Como Usar (Portabilidade)

1.  **Configurar PACS:** Informe IP, Porta e AE Titles no topo. Use o botão 📶 para validar.
2.  **Importar:** Selecione a pasta raiz. O sistema fará a leitura e preencherá automaticamente os dados do paciente.
3.  **Editar:** Use o **DATAMAKER** para corrigir dados. O sistema formatará as datas automaticamente para você.
4.  **Sincronizar:** Selecione as séries e envie para o destino com um clique.

---

## 🔧 Compilação e Deploy

### Para gerar o Executável Único (Portátil):
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true