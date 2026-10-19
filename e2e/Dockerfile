FROM mcr.microsoft.com/playwright:v1.64.1-noble
WORKDIR /workspace/e2e
COPY e2e/package.json e2e/package-lock.json ./
RUN npm ci
COPY e2e/ ./
COPY tests/fixtures/loanpro-products.csv /workspace/tests/fixtures/loanpro-products.csv
CMD ["npm", "test"]
