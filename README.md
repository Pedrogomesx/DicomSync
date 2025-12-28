# 🏥 DicomSync - Editor, Anonimizador e Cliente DICOM

![Status](https://img.shields.io/badge/Status-Estável-2ea44f)
![.NET](https://img.shields.io/badge/.NET-WPF-512bd4)
![License](https://img.shields.io/badge/License-MIT-blue)
![Arquitetura](https://img.shields.io/badge/Arquitetura-MVVM--Clean-orange)

**DicomSync** é uma ferramenta para gestão, edição técnica, anonimização e transmissão de imagens médicas (DICOM). Desenvolvida com foco em performance e integridade de dados, a aplicação abstrai a complexidade do protocolo DICOM em uma interface moderna, facilitando fluxos de trabalho em engenharia clínica e TI hospitalar.

---

## 📸 Screenshots

<div align="center">
  <img width="45%" src="https://github.com/user-attachments/assets/3752a730-8ccb-4980-85be-91f32f496ac6" alt="Tela Principal" />
  <img width="45%" src="https://github.com/user-attachments/assets/ef6f2b9b-ed55-425b-8a64-2a20cf2da188" alt="Editor DICOM" />
</div>

---

## 🚀 Funcionalidades Principais

### 1. 🛡️ Segurança e Anonimização (LGPD)
Funcionalidade crítica para compartilhamento de exames para ensino ou suporte técnico sem expor o paciente.
* **Remoção de PII:** Remove automaticamente nomes, IDs de pacientes e datas de nascimento.
* **Backup de Segurança:** O sistema cria uma cópia `BACKUP_ORIGINAL` antes de tocar em qualquer byte do arquivo original.
* **Sanitização de Tags:** Limpeza de metadados sensíveis conforme normas de conformidade.

### 2. ✏️ Editor DATAMAKER
* **Edição em Lote:** Altere o nome do paciente ou ID em centenas de imagens simultaneamente.
* **Máscaras Inteligentes:** Inputs com máscaras brasileiras (`dd/mm/yyyy`) que convertem automaticamente para o padrão DICOM (`yyyyMMdd`) no background.
* **Validação de Tags:** Impede a inserção de caracteres inválidos que quebrariam o envio para o PACS.

### 3. 📡 Conectividade PACS
* **C-ECHO (Ping DICOM):** Teste de conectividade real, validando TCP/IP e aceitação do AE Title.
* **C-STORE (Envio):** Motor de envio assíncrono com retentativas e log detalhado de falhas.
* **Feedback Visual:** Barras de progresso e contadores de sucesso/erro em tempo real.

### 4. 📂 Gestão de Arquivos
* **Varredura Recursiva:** Encontra arquivos DICOM (com ou sem extensão `.dcm`) em subpastas profundas.
* **Agrupamento Lógico:** Organiza arquivos soltos em uma estrutura de árvore por Paciente > Estudo > Série.

---

## 🏗️ Arquitetura e Engenharia

O projeto foi refatorado seguindo princípios de **Clean Architecture** e **MVVM**, visando desacoplamento e testabilidade.

* **Core/Services:** Camada pura que isola a biblioteca `fo-dicom` e regras de negócio.
* **UI/ViewModels:** Gerenciamento de estado reativo, sem código de lógica no `CodeBehind` do XAML.
* **Helpers:** Utilitários estáticos para manipulação de Tags DICOM e conversão de datas.
* **Self-Contained:** A aplicação não depende de instalação prévia do .NET Runtime na máquina do cliente.

---

## 🧪 Como Testar (Ambiente de Desenvolvimento)

Para testar o envio de imagens (C-STORE) sem possuir um PACS real, recomenda-se o uso de ferramentas de simulação:

1. **Baixe um Servidor de Teste:** Utilize o **HAPI TestPanel** ou **Orthanc Server**.
2. **Configure o Listener:** No simulador, abra uma porta (ex: `104` ou `11112`) e defina um AE Title (ex: `ANY-SCP`).
3. **Configure o DicomSync:**
   * IP: `127.0.0.1` (Localhost)
   * Porta: `11112` (A mesma do simulador)
   * AE Title: `ANY-SCP`
4. **Execute:** Clique no botão de teste (ícone de sinal) para validar o C-ECHO.

---

## 🔧 Compilação e Deploy

Para gerar um executável único (portátil) que roda em qualquer Windows 64bits:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true